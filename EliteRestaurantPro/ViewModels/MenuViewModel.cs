using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Services;

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

    private readonly AdminDataApiClient _data = new();

    private Dictionary<int, string> _inventoryNameById = new();

    private int? _editingProductId;
    private List<Product> _allProducts = [];
    private bool _isDialogOpen;
    private string _dialogTitle = "Add Product";
    private string _productName = string.Empty;
    private string _selectedCategory = "Drink";
    private string _selectedSubCategory = "Beer";
    private string _priceText = string.Empty;
    private string _productDescription = string.Empty;
    private string _productComposition = string.Empty;
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

    public string ProductDescription
    {
        get => _productDescription;
        set
        {
            if (!SetField(ref _productDescription, value))
                return;
            OnPropertyChanged(nameof(DescriptionCharCountRemaining));
        }
    }

    public string ProductComposition
    {
        get => _productComposition;
        set => SetField(ref _productComposition, value);
    }

    /// <summary>Characters remaining for customer description (max 350).</summary>
    public int DescriptionCharCountRemaining => Math.Max(0, 350 - (_productDescription ?? string.Empty).Length);

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
        EditProductCommand = new RelayCommand(product => _ = OpenEditDialogAsync(product as Product));
        DeleteProductCommand = new RelayCommand(product => _ = DeleteProductAsync(product as Product));
        SaveProductCommand = new RelayCommand(_ => _ = SaveProductAsync());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());

        UpdateSubCategories(SelectedCategory);
        InitializeViewCategories();
        _ = LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        Products.Clear();
        GroupedProducts.Clear();
        InventorySelections.Clear();

        try
        {
            var productsTask = _data.GetProductsAsync();
            var pisTask = _data.GetProductIngredientsAsync();
            var invTask = _data.GetInventoryItemsAsync();
            await Task.WhenAll(productsTask, pisTask, invTask).ConfigureAwait(true);

            var products = (await productsTask.ConfigureAwait(true))
                .OrderBy(p => p.Category)
                .ThenBy(p => p.SubCategory)
                .ThenBy(p => p.Name)
                .ToList();
            var pis = (await pisTask.ConfigureAwait(true)).ToList();
            var invList = (await invTask.ConfigureAwait(true)).ToList();
            var invById = invList.ToDictionary(i => i.Id);
            _inventoryNameById = invList.ToDictionary(i => i.Id, i => i.Name ?? string.Empty);

            foreach (var pi in pis)
            {
                if (invById.TryGetValue(pi.InventoryItemId, out var inv))
                    pi.InventoryItem = inv;
            }

            var pisByProduct = pis.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => (ICollection<ProductIngredient>)g.ToList());
            foreach (var p in products)
            {
                p.Ingredients = pisByProduct.TryGetValue(p.Id, out var list)
                    ? list
                    : new List<ProductIngredient>();
            }

            _allProducts = products;
            foreach (var product in products)
                Products.Add(product);

            InitializeViewCategories();
            UpdateViewSubCategories();
            RefreshGroupedProducts();

            foreach (var item in invList.OrderBy(i => i.Name))
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
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load menu from the cloud API.\n\n{ex.Message}",
                "Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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
                        subCategoryGroup.Products.Add(product);

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
               || Hit(p.Description)
               || Hit(p.Composition)
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
        ProductDescription = string.Empty;
        ProductComposition = string.Empty;
        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.Quantity = 1m;
        }
        IsDialogOpen = true;
    }

    private async Task<Dictionary<int, decimal>> GetIngredientQuantitiesForProductAsync(Product product)
    {
        if (product.Ingredients is { Count: > 0 })
        {
            return product.Ingredients
                .GroupBy(pi => pi.InventoryItemId)
                .ToDictionary(g => g.Key, g => g.First().Quantity);
        }

        var allPi = await _data.GetProductIngredientsAsync().ConfigureAwait(true);
        return allPi
            .Where(pi => pi.ProductId == product.Id)
            .GroupBy(pi => pi.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.First().Quantity);
    }

    private async Task OpenEditDialogAsync(Product? product)
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
        ProductDescription = product.Description ?? string.Empty;
        ProductComposition = product.Composition ?? string.Empty;
        OnPropertyChanged(nameof(DescriptionCharCountRemaining));

        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.Quantity = 1m;
        }

        try
        {
            var ingredientMap = await GetIngredientQuantitiesForProductAsync(product).ConfigureAwait(true);
            foreach (var ingredient in InventorySelections)
            {
                if (!ingredientMap.TryGetValue(ingredient.InventoryItemId, out var qty))
                    continue;
                ingredient.IsSelected = true;
                ingredient.Quantity = qty;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load ingredients for this product.\n\n{ex.Message}",
                "Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IsDialogOpen = true;
    }

    private async Task SaveProductAsync()
    {
        if (AppSession.IsStaffTablet) return;

        if (string.IsNullOrWhiteSpace(ProductName) ||
            string.IsNullOrWhiteSpace(SelectedCategory) ||
            string.IsNullOrWhiteSpace(SelectedSubCategory) ||
            !decimal.TryParse(PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            return;
        }

        var desc = (ProductDescription ?? string.Empty).Trim();
        if (desc.Length > 350)
            desc = desc[..350];
        var comp = (ProductComposition ?? string.Empty).Trim();

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

        try
        {
            if (_editingProductId is int productId)
            {
                var allPi = await _data.GetProductIngredientsAsync().ConfigureAwait(true);
                var pis = allPi
                    .Where(pi => pi.ProductId == productId)
                    .ToList();
                var ops = new List<CloudSyncOperation>();
                foreach (var pi in pis)
                    ops.Add(DesktopCloudPersistence.DeleteOperation(pi));

                var existing = _allProducts.First(p => p.Id == productId);
                existing.Name = ProductName.Trim();
                existing.Category = SelectedCategory.Trim();
                existing.SubCategory = SelectedSubCategory.Trim();
                existing.Price = price;
                existing.Description = string.IsNullOrWhiteSpace(desc) ? null : desc;
                existing.Composition = string.IsNullOrWhiteSpace(comp) ? null : comp;
                ops.Add(DesktopCloudPersistence.UpsertOperation(existing));
                DesktopCloudPersistence.PushBatchBlocking(ops);

                foreach (var ingredient in selectedIngredients)
                {
                    DesktopCloudPersistence.PushUpsertBlocking(new ProductIngredient
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

                var uniqueId = UniqueIdGenerator.NewId("MEN");
                var product = new Product
                {
                    UniqueId = uniqueId,
                    Name = ProductName.Trim(),
                    Category = SelectedCategory.Trim(),
                    SubCategory = SelectedSubCategory.Trim(),
                    Price = price,
                    Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                    Composition = string.IsNullOrWhiteSpace(comp) ? null : comp
                };

                DesktopCloudPersistence.PushUpsertBlocking(product);
                await LoadProductsAsync().ConfigureAwait(true);

                var created = _allProducts.FirstOrDefault(p => p.UniqueId == uniqueId);
                if (created is null)
                {
                    MessageBox.Show(
                        "The product was saved but could not be matched after refresh. Re-open the menu or try again.",
                        "Menu",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                foreach (var ingredient in selectedIngredients)
                {
                    DesktopCloudPersistence.PushUpsertBlocking(new ProductIngredient
                    {
                        ProductId = created.Id,
                        InventoryItemId = ingredient.InventoryItemId,
                        Quantity = ingredient.Quantity
                    });
                }
            }

            CloseDialog();
            await LoadProductsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save menu item to the cloud.\n\n{ex.Message}",
                "Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task DeleteProductAsync(Product? product)
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

        try
        {
            var allPi = await _data.GetProductIngredientsAsync().ConfigureAwait(true);
            var pis = allPi
                .Where(pi => pi.ProductId == product.Id)
                .ToList();
            var ops = pis.Select(DesktopCloudPersistence.DeleteOperation).ToList();
            ops.Add(DesktopCloudPersistence.DeleteOperation(product));
            DesktopCloudPersistence.PushBatchBlocking(ops);
            await LoadProductsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not delete menu item in the cloud.\n\n{ex.Message}",
                "Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public string GetIngredientSummary(Product product)
    {
        var names = (product.Ingredients ?? [])
            .Select(pi => pi.InventoryItem?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count > 0)
            return string.Join(", ", names);

        return "No ingredients linked";
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
