using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using EliteRestaurantPro.Data;
using EliteRestaurantPro.Models;
using EliteRestaurantPro.Utils;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurantPro.ViewModels;

public class MenuViewModel : AdminBaseViewModel
{
    public class MenuSubCategoryGroup
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<Product> Products { get; } = new();
    }

    public class MenuCategoryGroup
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<MenuSubCategoryGroup> SubCategories { get; } = new();
    }

    public class MenuTopGroup
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<MenuCategoryGroup> Categories { get; } = new();
    }

    private static readonly Dictionary<string, List<string>> CategoryMap = new()
    {
        ["Drink"] = new() { "Beer", "Champagne", "Cocktail", "Juice", "Mocktail", "Soft Drink", "Water", "Whisky" },
        ["Starter/Appetizer"] = new() { "Starter/Appetizer" },
        ["Main"] = new() { "Seafood", "Meat Meal", "Vegetarian", "Pasta", "Rice Dishes", "Grilled Meals", "Fast Food" },
        ["Dessert"] = new() { "Dessert" }
    };

    private int? _editingProductId;
    private List<Product> _allProducts = [];
    private bool _isDialogOpen;
    private string _dialogTitle = "Add Product";
    private string _productName = string.Empty;
    private string _selectedCategory = "Drink";
    private string _selectedSubCategory = "Beer";
    private string _priceText = string.Empty;
    private string _selectedViewCategory = "All";
    private string _selectedViewSubCategory = "All";
    private string _searchText = string.Empty;

    public override string ActivePage => "Menu";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            RefreshGroupedProducts();
        }
    }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<MenuTopGroup> GroupedProducts { get; } = new();
    public ObservableCollection<InventorySelectionItemViewModel> InventorySelections { get; } = new();
    public ObservableCollection<string> ProductIngredientsSummary { get; } = new();
    public ObservableCollection<string> SubCategories { get; } = new();
    public ObservableCollection<string> ViewCategories { get; } = new();
    public ObservableCollection<string> ViewSubCategories { get; } = new();
    public ObservableCollection<string> Categories { get; } =
        new(["Drink", "Starter/Appetizer", "Main", "Dessert"]);

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set => SetField(ref _isDialogOpen, value);
    }

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetField(ref _dialogTitle, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetField(ref _productName, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetField(ref _selectedCategory, value))
                return;
            UpdateSubCategories(value);
        }
    }

    public string SelectedSubCategory
    {
        get => _selectedSubCategory;
        set => SetField(ref _selectedSubCategory, value);
    }

    public string PriceText
    {
        get => _priceText;
        set => SetField(ref _priceText, value);
    }

    public string SelectedViewCategory
    {
        get => _selectedViewCategory;
        set
        {
            if (!SetField(ref _selectedViewCategory, value))
                return;
            UpdateViewSubCategories();
            RefreshGroupedProducts();
        }
    }

    public string SelectedViewSubCategory
    {
        get => _selectedViewSubCategory;
        set
        {
            if (!SetField(ref _selectedViewSubCategory, value))
                return;
            RefreshGroupedProducts();
        }
    }

    public ICommand OpenAddDialogCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand DeleteProductCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand CancelDialogCommand { get; }

    public MenuViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        EditProductCommand = new RelayCommand(product => OpenEditDialog(product as Product));
        DeleteProductCommand = new RelayCommand(product => DeleteProduct(product as Product));
        SaveProductCommand = new RelayCommand(_ => SaveProduct());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        UpdateSubCategories(SelectedCategory);
        InitializeViewCategories();
        LoadProducts();
    }

    private void LoadProducts()
    {
        Products.Clear();
        GroupedProducts.Clear();
        InventorySelections.Clear();

        using var db = new AppDbContext();
        var products = db.Products
                     .AsNoTracking()
                     .Include(p => p.Ingredients)
                     .ThenInclude(i => i.InventoryItem)
                     .OrderBy(p => p.Category)
                     .ThenBy(p => p.SubCategory)
                     .ThenBy(p => p.Name)
                     .ToList();

        _allProducts = products;
        foreach (var product in products)
        {
            Products.Add(product);
        }

        InitializeViewCategories();
        UpdateViewSubCategories();
        RefreshGroupedProducts();

        foreach (var item in db.InventoryItems.AsNoTracking().OrderBy(i => i.Name))
        {
            InventorySelections.Add(new InventorySelectionItemViewModel
            {
                InventoryItemId = item.Id,
                UniqueId = item.UniqueId,
                Name = item.Name,
                Unit = item.Unit,
                StockQuantity = item.StockQuantity,
                Quantity = 1m
            });
        }

        RefreshReadyPickupBanner();
    }

    private void BuildGroupedProducts(IEnumerable<Product> products)
    {
        GroupedProducts.Clear();
        foreach (var topSection in products
                     .GroupBy(p => GetTopSection(p.Category))
                     .OrderBy(g => g.Key))
        {
            var topGroup = new MenuTopGroup { Name = topSection.Key };

            foreach (var category in topSection
                         .GroupBy(p => p.Category)
                         .OrderBy(g => g.Key))
            {
                var categoryGroup = new MenuCategoryGroup { Name = category.Key };

                foreach (var subCategory in category
                             .GroupBy(p => string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory)
                             .OrderBy(g => g.Key))
                {
                    var subCategoryGroup = new MenuSubCategoryGroup { Name = subCategory.Key };
                    foreach (var product in subCategory)
                    {
                        subCategoryGroup.Products.Add(product);
                    }

                    categoryGroup.SubCategories.Add(subCategoryGroup);
                }

                topGroup.Categories.Add(categoryGroup);
            }

            GroupedProducts.Add(topGroup);
        }
    }

    private static string GetTopSection(string category) =>
        category.Equals("Drink", StringComparison.OrdinalIgnoreCase) ? "Drink" : "Food";

    private void RefreshGroupedProducts()
    {
        var q = (_searchText ?? string.Empty).Trim();
        var filtered = _allProducts
            .Where(p => SelectedViewCategory == "All" || p.Category.Equals(SelectedViewCategory, StringComparison.OrdinalIgnoreCase))
            .Where(p => SelectedViewSubCategory == "All" || p.SubCategory.Equals(SelectedViewSubCategory, StringComparison.OrdinalIgnoreCase))
            .Where(p => MenuProductMatchesSearch(p, q))
            .ToList();

        BuildGroupedProducts(filtered);
    }

    private static bool MenuProductMatchesSearch(Product p, string q)
    {
        if (q.Length == 0)
            return true;

        bool Hit(string? s)
            => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var sub = string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory;
        var priceText = p.Price.ToString("0.00", CultureInfo.InvariantCulture);
        return Hit(p.Name)
               || Hit(p.Category)
               || Hit(sub)
               || Hit(p.UniqueId)
               || priceText.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void InitializeViewCategories()
    {
        var allCategories = Products
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        if (allCategories.Count == 0)
            allCategories = Categories.ToList();

        ViewCategories.Clear();
        ViewCategories.Add("All");
        foreach (var category in allCategories)
            ViewCategories.Add(category);

        if (!ViewCategories.Contains(SelectedViewCategory))
            SelectedViewCategory = "All";
    }

    private void UpdateViewSubCategories()
    {
        var subCategories = _allProducts
            .Where(p => SelectedViewCategory == "All" || p.Category.Equals(SelectedViewCategory, StringComparison.OrdinalIgnoreCase))
            .Select(p => string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        ViewSubCategories.Clear();
        ViewSubCategories.Add("All");
        foreach (var subCategory in subCategories)
            ViewSubCategories.Add(subCategory);

        if (!ViewSubCategories.Contains(SelectedViewSubCategory))
            SelectedViewSubCategory = "All";
    }

    private void OpenAddDialog()
    {
        if (AppSession.IsStaffTablet) return;

        _editingProductId = null;
        DialogTitle = "Add Product";
        ProductName = string.Empty;
        SelectedCategory = Categories.First();
        SelectedSubCategory = SubCategories.FirstOrDefault() ?? string.Empty;
        PriceText = string.Empty;
        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.Quantity = 1m;
        }
        IsDialogOpen = true;
    }

    private void OpenEditDialog(Product? product)
    {
        if (product is null) return;
        if (AppSession.IsStaffTablet) return;

        _editingProductId = product.Id;
        DialogTitle = "Edit Product";
        ProductName = product.Name;
        SelectedCategory = product.Category;
        SelectedSubCategory = string.IsNullOrWhiteSpace(product.SubCategory)
            ? SubCategories.FirstOrDefault() ?? string.Empty
            : product.SubCategory;
        PriceText = product.Price.ToString("0.00", CultureInfo.InvariantCulture);

        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.Quantity = 1m;
        }

        using var db = new AppDbContext();
        var ingredientMap = db.ProductIngredients
            .AsNoTracking()
            .Where(pi => pi.ProductId == product.Id)
            .ToDictionary(pi => pi.InventoryItemId, pi => pi.Quantity);

        foreach (var ingredient in InventorySelections)
        {
            if (!ingredientMap.TryGetValue(ingredient.InventoryItemId, out var qty))
                continue;
            ingredient.IsSelected = true;
            ingredient.Quantity = qty;
        }

        IsDialogOpen = true;
    }

    private void SaveProduct()
    {
        if (AppSession.IsStaffTablet) return;

        if (string.IsNullOrWhiteSpace(ProductName) ||
            string.IsNullOrWhiteSpace(SelectedCategory) ||
            string.IsNullOrWhiteSpace(SelectedSubCategory) ||
            !decimal.TryParse(PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            return;
        }

        using var db = new AppDbContext();
        var selectedIngredients = InventorySelections.Where(i => i.IsSelected).ToList();
        if (!selectedIngredients.Any())
        {
            MessageBox.Show(
                "Select at least one inventory ingredient for this menu item.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_editingProductId is int productId)
        {
            var existing = db.Products.Single(p => p.Id == productId);
            existing.Name = ProductName.Trim();
            existing.Category = SelectedCategory.Trim();
            existing.SubCategory = SelectedSubCategory.Trim();
            existing.Price = price;

            var existingIngredients = db.ProductIngredients.Where(pi => pi.ProductId == productId);
            db.ProductIngredients.RemoveRange(existingIngredients);

            foreach (var ingredient in selectedIngredients)
            {
                db.ProductIngredients.Add(new ProductIngredient
                {
                    ProductId = productId,
                    InventoryItemId = ingredient.InventoryItemId,
                    Quantity = ingredient.Quantity
                });
            }
        }
        else
        {
            var confirm = MessageBox.Show(
                "Add this menu item?",
                "Confirm Add Menu Item",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            var product = new Product
            {
                UniqueId = UniqueIdGenerator.NewId("MEN"),
                Name = ProductName.Trim(),
                Category = SelectedCategory.Trim(),
                SubCategory = SelectedSubCategory.Trim(),
                Price = price
            };

            db.Products.Add(product);
            db.SaveChanges();

            foreach (var ingredient in selectedIngredients)
            {
                db.ProductIngredients.Add(new ProductIngredient
                {
                    ProductId = product.Id,
                    InventoryItemId = ingredient.InventoryItemId,
                    Quantity = ingredient.Quantity
                });
            }
        }

        db.SaveChanges();
        CloseDialog();
        LoadProducts();
    }

    private void DeleteProduct(Product? product)
    {
        if (product is null) return;
        if (AppSession.IsStaffTablet) return;

        var confirmDelete = MessageBox.Show(
            $"Delete menu item '{product.Name}'?",
            "Confirm Delete Menu Item",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmDelete != MessageBoxResult.Yes)
            return;

        using var db = new AppDbContext();
        var existing = db.Products.Include(p => p.Ingredients).SingleOrDefault(p => p.Id == product.Id);
        if (existing is null) return;

        db.ProductIngredients.RemoveRange(existing.Ingredients);
        db.Products.Remove(existing);
        db.SaveChanges();
        LoadProducts();
    }

    public string GetIngredientSummary(Product product)
    {
        using var db = new AppDbContext();
        var ingredients = db.ProductIngredients
            .AsNoTracking()
            .Include(i => i.InventoryItem)
            .Where(i => i.ProductId == product.Id)
            .Select(i => i.InventoryItem != null ? i.InventoryItem.Name : string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return ingredients.Count == 0 ? "No ingredients linked" : string.Join(", ", ingredients);
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingProductId = null;
    }

    private void UpdateSubCategories(string category)
    {
        SubCategories.Clear();

        if (!CategoryMap.TryGetValue(category, out var subCategories))
            subCategories = new List<string> { category };

        foreach (var item in subCategories)
            SubCategories.Add(item);

        if (SubCategories.Count == 0)
        {
            SelectedSubCategory = string.Empty;
            return;
        }

        if (!SubCategories.Contains(SelectedSubCategory))
            SelectedSubCategory = SubCategories.First();
    }
}
