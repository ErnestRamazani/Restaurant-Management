using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Menu;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Utils;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.Services;
using EliteRestaurantPro.Utils;
using Microsoft.Win32;

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

    private static readonly Dictionary<string, List<string>> LegacyCategoryMap = new()
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
    private string _prepMinutesText = string.Empty;
    private string _productDescription = string.Empty;
    private string _productComposition = string.Empty;
    private string _selectedViewCategory = "All";
    private string _selectedViewSubCategory = "All";
    private LocalizedSelectOption? _selectedViewCategoryOption;
    private LocalizedSelectOption? _selectedViewSubCategoryOption;
    private Product? _detailsProduct;
    private string _searchText = string.Empty;
    private bool _isDetailsDialogMode;
    private string _detailsProductName = string.Empty;
    private string _detailsUniqueId = string.Empty;
    private string _detailsCategory = string.Empty;
    private string _detailsSubCategory = string.Empty;
    private string _detailsPrice = string.Empty;
    private string _detailsPrepMinutes = string.Empty;
    private string _detailsDescription = string.Empty;
    private string _detailsComposition = string.Empty;
    private string _productImagePath = string.Empty;
    private bool _removeProductImage;
    private ImageSource? _editorImagePreview;
    private ImageSource? _detailsImagePreview;
    private string _ingredientSearchText = string.Empty;

    public ICollectionView FilteredInventoryView { get; }

    public string PageTitle => Loc.Admin("menuTitle", "Menu Products");
    public string PageSubtitle => Loc.Admin("menuSubtitle", "Curate the premium product catalog for service and POS.");
    public string AddProductLabel => Loc.Admin("menuAddProduct", "Add Product");
    public string MenuTypeLabel => Loc.Admin("menuMenuTypeLabel", "MENU TYPE");
    public string SubmenuTypeLabel => Loc.Admin("menuSubmenuTypeLabel", "SUBMENU TYPE");
    public string SearchTooltip => Loc.Admin("menuSearchTooltip", "Search menu items by name, category, ID, or price");
    public string DetailsLabel => Loc.Admin("menuDetails", "Details");
    public string EditLabel => Loc.Admin("menuEdit", "Edit");
    public string DeleteLabel => Loc.Admin("menuDelete", "Delete");
    public string MenuFieldProductNameLabel => Loc.Admin("menuFieldProductName", "PRODUCT NAME");
    public string MenuFieldSectionLabel => Loc.Admin("menuFieldSection", "SECTION");
    public string MenuFieldTypeLabel => Loc.Admin("menuFieldType", "TYPE");
    public string MenuFieldPriceLabel => Loc.Admin("menuFieldPrice", "PRICE");
    public string MenuFieldCookingTimeLabel => Loc.Admin("menuFieldCookingTime", "COOKING TIME (MINUTES)");
    public string MenuFieldCookingTimeHelp => Loc.Admin("menuFieldCookingTimeHelp",
        "Used for kitchen estimates on guest menu, POS, and server tablets. Required for each menu item.");
    public string MenuFieldDescriptionLabel => Loc.Admin("menuFieldDescription", "DESCRIPTION (CUSTOMER MENU)");
    public string MenuFieldCompositionLabel => Loc.Admin("menuFieldComposition", "COMPOSITION (CUSTOMER, COMMA-SEPARATED)");
    public string MenuDetailsDescriptionLabel => Loc.Admin("menuDetailsDescriptionLabel", "DESCRIPTION");
    public string MenuDetailsCompositionLabel => Loc.Admin("menuDetailsCompositionLabel", "COMPOSITION");
    public string MenuFieldProductImageLabel => Loc.Admin("menuFieldProductImage", "PRODUCT IMAGE");
    public string BrowseLabel => Loc.Admin("menuBrowse", "Browse");
    public string ClearLabel => Loc.Admin("menuClear", "Clear");
    public string MenuNoImageSelectedLabel => Loc.Admin("menuNoImageSelected", "No image selected");
    public string MenuInventoryIngredientsLabel => Loc.Admin("menuInventoryIngredients", "Ingredients from inventory");
    public string MenuStockPrefix => Loc.Admin("menuStockPrefix", "Stock:");
    public string MenuSaveProductLabel => Loc.Admin("menuSaveProduct", "Save Product");
    public string CancelLabel => Loc.Common("cancel", "Cancel");
    public string CloseLabel => Loc.Common("close", "Close");
    public string MenuNoImageAvailableLabel => Loc.Admin("menuNoImageAvailable", "No image available");
    public string MenuFieldProductIdLabel => Loc.Admin("menuFieldProductId", "PRODUCT ID");
    public string MenuFieldCookingTimeShortLabel => Loc.Admin("menuFieldCookingTimeShort", "COOKING TIME");
    public string MenuIngredientsLabel => Loc.Admin("menuIngredientsLabel", "Ingredients");
    public string MenuIngredientsPanelTitle => Loc.Admin("menuIngredientsPanelTitle", "Recipe & inventory");
    public string MenuIngredientsPanelHint => Loc.Admin("menuIngredientsPanelHint",
        "Link stock items and quantities used for one serving. Search by name or SKU.");
    public string MenuIngredientSearchPlaceholder => Loc.Admin("menuIngredientSearchPlaceholder", "Search ingredients…");
    public string MenuEditorDialogSubtitle => Loc.Admin("menuEditorDialogSubtitle",
        "Complete the dish profile and link inventory for costing and stock deduction.");
    public string MenuIngredientQtyLabel => Loc.Admin("menuIngredientQtyLabel", "Qty");
    public string SelectedIngredientsCountText => Loc.Admin("menuIngredientsSelectedCount", "{{count}} selected",
        new Dictionary<string, string>
        {
            ["count"] = SelectedIngredientsCount.ToString(CultureInfo.InvariantCulture)
        });
    public string SelectedIngredientsSummaryText
    {
        get
        {
            var selected = InventorySelections.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
                return Loc.Admin("menuNoIngredientsSelected", "No ingredients selected yet.");
            return string.Join(", ",
                selected.Select(i =>
                    $"{i.Name} ({i.Quantity:0.##} {i.Unit})"));
        }
    }

    public int SelectedIngredientsCount => InventorySelections.Count(i => i.IsSelected);

    public string IngredientSearchText
    {
        get => _ingredientSearchText;
        set
        {
            if (!SetField(ref _ingredientSearchText, value))
                return;
            FilteredInventoryView.Refresh();
        }
    }

    public override string ActivePage => "Menu";

    public string DescriptionCharCountRemainingText => Loc.Admin("menuCharactersRemaining", "Characters remaining: {{count}}",
        new Dictionary<string, string>
        {
            ["count"] = DescriptionCharCountRemaining.ToString(CultureInfo.InvariantCulture)
        });

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
    public ObservableCollection<LocalizedSelectOption> ViewCategoryOptions { get; } = new();
    public ObservableCollection<LocalizedSelectOption> ViewSubCategoryOptions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set
        {
            if (!SetField(ref _isDialogOpen, value))
                return;
            OnPropertyChanged(nameof(IsMainContentEnabled));
        }
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

    public string PrepMinutesText
    {
        get => _prepMinutesText;
        set => SetField(ref _prepMinutesText, value);
    }

    public string ProductDescription
    {
        get => _productDescription;
        set
        {
            if (!SetField(ref _productDescription, value))
                return;
            OnPropertyChanged(nameof(DescriptionCharCountRemaining));
            OnPropertyChanged(nameof(DescriptionCharCountRemainingText));
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
            SyncViewCategoryOption();
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
            SyncViewSubCategoryOption();
            RefreshGroupedProducts();
        }
    }

    public LocalizedSelectOption? SelectedViewCategoryOption
    {
        get => _selectedViewCategoryOption;
        set
        {
            if (!SetField(ref _selectedViewCategoryOption, value) || value is null)
                return;
            SelectedViewCategory = value.Value;
        }
    }

    public LocalizedSelectOption? SelectedViewSubCategoryOption
    {
        get => _selectedViewSubCategoryOption;
        set
        {
            if (!SetField(ref _selectedViewSubCategoryOption, value) || value is null)
                return;
            SelectedViewSubCategory = value.Value;
        }
    }

    public bool IsDetailsDialogMode
    {
        get => _isDetailsDialogMode;
        set
        {
            if (!SetField(ref _isDetailsDialogMode, value))
                return;
            OnPropertyChanged(nameof(IsEditorDialogMode));
        }
    }

    public bool IsEditorDialogMode => !IsDetailsDialogMode;
    public bool IsMainContentEnabled => !IsDialogOpen;

    public string DetailsProductName
    {
        get => _detailsProductName;
        set => SetField(ref _detailsProductName, value);
    }

    public string DetailsUniqueId
    {
        get => _detailsUniqueId;
        set => SetField(ref _detailsUniqueId, value);
    }

    public string DetailsCategory
    {
        get => _detailsCategory;
        set => SetField(ref _detailsCategory, value);
    }

    public string DetailsSubCategory
    {
        get => _detailsSubCategory;
        set => SetField(ref _detailsSubCategory, value);
    }

    public string DetailsPrice
    {
        get => _detailsPrice;
        set => SetField(ref _detailsPrice, value);
    }

    public string DetailsPrepMinutes
    {
        get => _detailsPrepMinutes;
        set => SetField(ref _detailsPrepMinutes, value);
    }

    public string DetailsDescription
    {
        get => _detailsDescription;
        set => SetField(ref _detailsDescription, value);
    }

    public string DetailsComposition
    {
        get => _detailsComposition;
        set => SetField(ref _detailsComposition, value);
    }

    public string ProductImagePath
    {
        get => _productImagePath;
        set
        {
            if (!SetField(ref _productImagePath, value))
                return;
            _removeProductImage = false;
            RefreshEditorImagePreview();
        }
    }

    public bool HasEditorImagePreview => _editorImagePreview is not null;
    public bool HasNoEditorImagePreview => !HasEditorImagePreview;
    public ImageSource? EditorImagePreview
    {
        get => _editorImagePreview;
        private set
        {
            if (SetField(ref _editorImagePreview, value))
            {
                OnPropertyChanged(nameof(HasEditorImagePreview));
                OnPropertyChanged(nameof(HasNoEditorImagePreview));
            }
        }
    }

    public bool HasDetailsImagePreview => _detailsImagePreview is not null;
    public bool HasNoDetailsImagePreview => !HasDetailsImagePreview;
    public ImageSource? DetailsImagePreview
    {
        get => _detailsImagePreview;
        private set
        {
            if (SetField(ref _detailsImagePreview, value))
            {
                OnPropertyChanged(nameof(HasDetailsImagePreview));
                OnPropertyChanged(nameof(HasNoDetailsImagePreview));
            }
        }
    }

    public ICommand OpenAddDialogCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand DeleteProductCommand { get; }
    public ICommand ShowProductDetailsCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand CancelDialogCommand { get; }
    public ICommand BrowseProductImageCommand { get; }
    public ICommand ClearProductImageCommand { get; }

    public MenuViewModel(Action<BaseViewModel> navigate) : base(navigate)
    {
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        EditProductCommand = new RelayCommand(product => _ = OpenEditDialogAsync(product as Product));
        DeleteProductCommand = new RelayCommand(product => _ = DeleteProductAsync(product as Product));
        ShowProductDetailsCommand = new RelayCommand(product => OpenDetailsDialog(product as Product));
        SaveProductCommand = new RelayCommand(_ => _ = SaveProductAsync());
        CancelDialogCommand = new RelayCommand(_ => CloseDialog());
        BrowseProductImageCommand = new RelayCommand(_ => BrowseProductImage());
        ClearProductImageCommand = new RelayCommand(_ => ClearProductImage());

        FilteredInventoryView = CollectionViewSource.GetDefaultView(InventorySelections);
        FilteredInventoryView.Filter = FilterInventoryItem;

        SettingsManager.SettingsChanged += OnMenuSettingsChanged;

        RebuildCategoryUiFromSettings();
        UpdateSubCategories(SelectedCategory);
        _ = LoadProductsAsync();
    }

    private void OnMenuSettingsChanged()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (dispatcher.CheckAccess())
            ApplyMenuSettingsFromDisk();
        else
            dispatcher.BeginInvoke(ApplyMenuSettingsFromDisk);
    }

    private void ApplyMenuSettingsFromDisk()
    {
        RebuildCategoryUiFromSettings();
        InitializeViewCategories();
        UpdateViewSubCategories();
        RefreshGroupedProducts();
    }

    private void RebuildCategoryUiFromSettings()
    {
        var tax = MenuTaxonomyHelper.Resolve(SettingsManager.Load().MenuTaxonomy);
        var ordered = MenuTaxonomyHelper.GetOrderedSectionNames(tax).ToList();
        foreach (var p in _allProducts)
        {
            var c = (p.Category ?? string.Empty).Trim();
            if (c.Length > 0 && !ordered.Any(o => o.Equals(c, StringComparison.OrdinalIgnoreCase)))
                ordered.Add(c);
        }

        Categories.Clear();
        foreach (var c in ordered)
            Categories.Add(c);

        if (Categories.Count == 0)
        {
            foreach (var x in new[] { "Alcohol", "Non-Alcohol", "Starter/Appetizer", "Main", "Dessert" })
                Categories.Add(x);
        }

        if (!Categories.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
            SelectedCategory = Categories.FirstOrDefault() ?? "Main";

        UpdateSubCategories(SelectedCategory);
    }

    private async Task LoadProductsAsync()
    {
        MenuImagePreview.ClearRemoteImageCache();
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

            await Task.Run(() => MenuImagePreview.PrefetchProductPhotoUrls(products.Select(p => p.Id)))
                .ConfigureAwait(true);

            foreach (var product in products)
                Products.Add(product);

            RebuildCategoryUiFromSettings();
            InitializeViewCategories();
            UpdateViewSubCategories();
            RefreshGroupedProducts();

            foreach (var item in invList.OrderBy(i => i.Name))
            {
                var row = new InventorySelectionItemViewModel
                {
                    InventoryItemId = item.Id,
                    UniqueId = item.UniqueId,
                    Name = item.Name,
                    Unit = item.Unit,
                    StockQuantity = item.StockQuantity
                };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(InventorySelectionItemViewModel.IsSelected)
                        or nameof(InventorySelectionItemViewModel.Quantity))
                        RefreshIngredientSelectionSummary();
                };
                row.ResetQuantity(1m);
                InventorySelections.Add(row);
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
                     .GroupBy(p => MenuTaxonomyHelper.GetTypeNameForProductOrFallback(
                         p,
                         MenuTaxonomyHelper.Resolve(SettingsManager.Load().MenuTaxonomy)))
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
        var tax = MenuTaxonomyHelper.Resolve(SettingsManager.Load().MenuTaxonomy);
        var ordered = MenuTaxonomyHelper.GetOrderedSectionNames(tax).ToList();
        foreach (var p in Products)
        {
            var c = (p.Category ?? string.Empty).Trim();
            if (c.Length > 0 && !ordered.Any(o => o.Equals(c, StringComparison.OrdinalIgnoreCase)))
                ordered.Add(c);
        }

        if (ordered.Count == 0)
        {
            foreach (var c in Categories)
                ordered.Add(c);
        }

        var current = _selectedViewCategory;
        ViewCategoryOptions.Clear();
        ViewCategoryOptions.Add(new LocalizedSelectOption
        {
            Value = "All",
            Label = Loc.Admin("menuAllTypes", "All types")
        });
        foreach (var category in ordered)
        {
            ViewCategoryOptions.Add(new LocalizedSelectOption
            {
                Value = category,
                Label = category
            });
        }

        if (!ViewCategoryOptions.Any(o => o.Value.Equals(current, StringComparison.OrdinalIgnoreCase)))
            current = "All";

        _selectedViewCategory = current;
        OnPropertyChanged(nameof(SelectedViewCategory));
        SyncViewCategoryOption();
        UpdateViewSubCategories();
    }

    private void UpdateViewSubCategories()
    {
        var subCategories = _allProducts
            .Where(p => SelectedViewCategory == "All" || p.Category.Equals(SelectedViewCategory, StringComparison.OrdinalIgnoreCase))
            .Select(p => string.IsNullOrWhiteSpace(p.SubCategory) ? "General" : p.SubCategory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var current = _selectedViewSubCategory;
        ViewSubCategoryOptions.Clear();
        ViewSubCategoryOptions.Add(new LocalizedSelectOption
        {
            Value = "All",
            Label = Loc.Admin("menuAllSubtypes", "All subtypes")
        });
        foreach (var subCategory in subCategories)
        {
            ViewSubCategoryOptions.Add(new LocalizedSelectOption
            {
                Value = subCategory,
                Label = subCategory
            });
        }

        if (!ViewSubCategoryOptions.Any(o => o.Value.Equals(current, StringComparison.OrdinalIgnoreCase)))
            current = "All";

        _selectedViewSubCategory = current;
        OnPropertyChanged(nameof(SelectedViewSubCategory));
        SyncViewSubCategoryOption();
    }

    private void SyncViewCategoryOption()
    {
        var match = ViewCategoryOptions.FirstOrDefault(o => o.Value.Equals(_selectedViewCategory, StringComparison.OrdinalIgnoreCase))
                    ?? ViewCategoryOptions.FirstOrDefault();
        if (ReferenceEquals(_selectedViewCategoryOption, match))
            return;
        _selectedViewCategoryOption = match;
        OnPropertyChanged(nameof(SelectedViewCategoryOption));
    }

    private void SyncViewSubCategoryOption()
    {
        var match = ViewSubCategoryOptions.FirstOrDefault(o => o.Value.Equals(_selectedViewSubCategory, StringComparison.OrdinalIgnoreCase))
                    ?? ViewSubCategoryOptions.FirstOrDefault();
        if (ReferenceEquals(_selectedViewSubCategoryOption, match))
            return;
        _selectedViewSubCategoryOption = match;
        OnPropertyChanged(nameof(SelectedViewSubCategoryOption));
    }

    private void OpenAddDialog()
    {
        if (AppSession.IsStaffTablet) return;

        IsDetailsDialogMode = false;
        _editingProductId = null;
        DialogTitle = Loc.Admin("menuAddDialog", "Add Product");
        ProductName = string.Empty;
        SelectedCategory = Categories.First();
        SelectedSubCategory = SubCategories.FirstOrDefault() ?? string.Empty;
        PriceText = string.Empty;
        PrepMinutesText = string.Empty;
        ProductDescription = string.Empty;
        ProductComposition = string.Empty;
        ProductImagePath = string.Empty;
        _removeProductImage = false;
        DetailsImagePreview = null;
        ResetIngredientSearch();
        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.ResetQuantity(1m);
        }
        IsDialogOpen = true;
        RefreshIngredientSelectionSummary();
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

        IsDetailsDialogMode = false;
        _editingProductId = product.Id;
        DialogTitle = Loc.Admin("menuEditDialog", "Edit Product");
        ProductName = product.Name;
        SelectedCategory = product.Category;
        SelectedSubCategory = string.IsNullOrWhiteSpace(product.SubCategory)
            ? SubCategories.FirstOrDefault() ?? string.Empty
            : product.SubCategory;
        PriceText = product.Price.ToString("0.00", CultureInfo.InvariantCulture);
        PrepMinutesText = product.PrepMinutes > 0
            ? product.PrepMinutes.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        ProductDescription = product.Description ?? string.Empty;
        ProductComposition = product.Composition ?? string.Empty;
        ProductImagePath = string.Empty;
        _removeProductImage = false;
        DetailsImagePreview = null;
        OnPropertyChanged(nameof(DescriptionCharCountRemaining));
        OnPropertyChanged(nameof(DescriptionCharCountRemainingText));
        RefreshEditorImagePreview();

        ResetIngredientSearch();
        foreach (var ingredient in InventorySelections)
        {
            ingredient.IsSelected = false;
            ingredient.ResetQuantity(1m);
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
        RefreshIngredientSelectionSummary();
    }

    private void OpenDetailsDialog(Product? product)
    {
        if (product is null)
            return;

        IsDetailsDialogMode = true;
        _editingProductId = null;
        _detailsProduct = product;
        DialogTitle = Loc.Admin("menuDetailsTitle", "Product Details");
        ApplyDetailsDisplayFields(product);
        IsDialogOpen = true;
    }

    private void ApplyDetailsDisplayFields(Product product)
    {
        DetailsProductName = string.IsNullOrWhiteSpace(product.Name)
            ? Loc.Admin("menuUnnamedProduct", "Unnamed product")
            : product.Name;
        DetailsUniqueId = string.IsNullOrWhiteSpace(product.UniqueId)
            ? Loc.Admin("menuNotApplicable", "N/A")
            : product.UniqueId;
        DetailsCategory = string.IsNullOrWhiteSpace(product.Category)
            ? Loc.Admin("menuUncategorized", "Uncategorized")
            : product.Category;
        DetailsSubCategory = string.IsNullOrWhiteSpace(product.SubCategory)
            ? Loc.Admin("menuGeneral", "General")
            : product.SubCategory;
        DetailsPrice = product.Price.ToString("N2", CultureInfo.InvariantCulture);
        DetailsPrepMinutes = product.PrepMinutes > 0
            ? Loc.Admin("menuPrepMinutesFmt", "{{minutes}} min",
                new Dictionary<string, string>
                {
                    ["minutes"] = product.PrepMinutes.ToString(CultureInfo.InvariantCulture)
                })
            : Loc.Admin("menuPrepNotSet", "Not set (category estimate used on orders)");
        DetailsDescription = string.IsNullOrWhiteSpace(product.Description)
            ? Loc.Admin("menuNoDescription", "No description provided.")
            : product.Description.Trim();
        DetailsComposition = string.IsNullOrWhiteSpace(product.Composition)
            ? Loc.Admin("menuNoComposition", "No composition provided.")
            : product.Composition.Trim();
        DetailsImagePreview = MenuImagePreview.TryLoadFromPathOrUrl(MenuImagePreview.GetProductPhotoAssetUrl(product.Id));

        ProductIngredientsSummary.Clear();
        foreach (var ingredient in (product.Ingredients ?? [])
                     .Where(i => i is not null)
                     .OrderBy(i => i?.InventoryItem?.Name))
        {
            if (ingredient is null)
                continue;

            var ingredientName = ingredient.InventoryItem?.Name;
            if (string.IsNullOrWhiteSpace(ingredientName) &&
                _inventoryNameById.TryGetValue(ingredient.InventoryItemId, out var cached))
            {
                ingredientName = cached;
            }

            if (string.IsNullOrWhiteSpace(ingredientName))
            {
                ingredientName = Loc.Admin("menuInventoryItemFallback", "Inventory #{{id}}",
                    new Dictionary<string, string>
                    {
                        ["id"] = ingredient.InventoryItemId.ToString(CultureInfo.InvariantCulture)
                    });
            }

            var unit = ingredient.InventoryItem?.Unit;
            var unitSuffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
            ProductIngredientsSummary.Add($"{ingredientName} - {ingredient.Quantity:0.##}{unitSuffix}");
        }

        if (ProductIngredientsSummary.Count == 0)
            ProductIngredientsSummary.Add(Loc.Admin("menuNoInventoryIngredients", "No inventory ingredients linked."));

        OnPropertyChanged(nameof(DetailsIngredientsPanelSubtitle));
    }

    public string DetailsIngredientsPanelSubtitle
    {
        get
        {
            var count = (_detailsProduct?.Ingredients ?? []).Count;
            if (count == 0)
                return Loc.Admin("menuNoInventoryIngredients", "No inventory ingredients linked.");
            return Loc.Admin("menuDetailsIngredientCount", "{{count}} linked from inventory",
                new Dictionary<string, string>
                {
                    ["count"] = count.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private async Task SaveProductAsync()
    {
        if (AppSession.IsStaffTablet) return;

        if (string.IsNullOrWhiteSpace(ProductName) ||
            string.IsNullOrWhiteSpace(SelectedCategory) ||
            string.IsNullOrWhiteSpace(SelectedSubCategory) ||
            !decimal.TryParse(PriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            MessageBox.Show(
                "Enter product name, section, type, and a valid price.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!int.TryParse((PrepMinutesText ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var prepMinutes)
            || prepMinutes < 1
            || prepMinutes > 480)
        {
            MessageBox.Show(
                "Cooking time must be a whole number between 1 and 480 minutes.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var desc = (ProductDescription ?? string.Empty).Trim();
        if (desc.Length > 350)
            desc = desc[..350];
        var comp = (ProductComposition ?? string.Empty).Trim();

        var selectedIngredients = InventorySelections.Where(i => i.IsSelected).ToList();
        foreach (var ingredient in InventorySelections.Where(i => i.IsSelected))
            ingredient.CommitQuantityFromText();
        selectedIngredients = InventorySelections.Where(i => i.IsSelected).ToList();
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
                existing.PrepMinutes = prepMinutes;
                existing.Description = string.IsNullOrWhiteSpace(desc) ? null : desc;
                existing.Composition = string.IsNullOrWhiteSpace(comp) ? null : comp;
                ops.Add(DesktopCloudPersistence.UpsertOperation(existing));
                if (_removeProductImage)
                {
                    ops.Add(DesktopCloudPersistence.DeleteOperation(new PublicMenuAsset
                    {
                        Key = ProductPhotoAssetKey(productId)
                    }));
                }
                DesktopCloudPersistence.PushBatchBlocking(ops);
                UpsertProductImageAssetIfSelected(productId);

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
                    PrepMinutes = prepMinutes,
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
                UpsertProductImageAssetIfSelected(created.Id);
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
            ops.Add(DesktopCloudPersistence.DeleteOperation(new PublicMenuAsset
            {
                Key = ProductPhotoAssetKey(product.Id)
            }));
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

        return Loc.Admin("menuNoIngredientsLinked", "No ingredients linked");
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
        _editingProductId = null;
        _detailsProduct = null;
        ProductImagePath = string.Empty;
        _removeProductImage = false;
        EditorImagePreview = null;
        DetailsImagePreview = null;
        ResetIngredientSearch();
    }

    private void ResetIngredientSearch()
    {
        if (string.IsNullOrEmpty(_ingredientSearchText))
            return;
        _ingredientSearchText = string.Empty;
        OnPropertyChanged(nameof(IngredientSearchText));
        FilteredInventoryView.Refresh();
    }

    private bool FilterInventoryItem(object obj)
    {
        if (obj is not InventorySelectionItemViewModel item)
            return false;

        var query = (_ingredientSearchText ?? string.Empty).Trim();
        if (query.Length == 0)
            return true;

        return item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.UniqueId.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshIngredientSelectionSummary()
    {
        OnPropertyChanged(nameof(SelectedIngredientsCount));
        OnPropertyChanged(nameof(SelectedIngredientsCountText));
        OnPropertyChanged(nameof(SelectedIngredientsSummaryText));
    }

    private void BrowseProductImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select product image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            ProductImagePath = dialog.FileName;
    }

    private void ClearProductImage()
    {
        ProductImagePath = string.Empty;
        _removeProductImage = _editingProductId is int;
        RefreshEditorImagePreview();
    }

    private void RefreshEditorImagePreview()
    {
        if (!string.IsNullOrWhiteSpace(ProductImagePath))
        {
            EditorImagePreview = MenuImagePreview.TryLoadFromPathOrUrl(ProductImagePath);
            return;
        }

        if (!_removeProductImage && _editingProductId is int id)
        {
            EditorImagePreview = MenuImagePreview.TryLoadFromPathOrUrl(MenuImagePreview.GetProductPhotoAssetUrl(id));
            return;
        }

        EditorImagePreview = null;
    }

    private void UpsertProductImageAssetIfSelected(int productId)
    {
        var path = (ProductImagePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > 4 * 1024 * 1024)
            return;

        DesktopCloudPersistence.PushUpsertBlocking(new PublicMenuAsset
        {
            Key = ProductPhotoAssetKey(productId),
            FileName = Path.GetFileName(path),
            ContentType = GuessImageContentType(path),
            Content = bytes,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private static string ProductPhotoAssetKey(int productId) => $"product:{productId}";

    private static string GuessImageContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };

    private void UpdateSubCategories(string category)
    {
        SubCategories.Clear();
        var map = MenuTaxonomyHelper.GetCategoryEditorMap(MenuTaxonomyHelper.Resolve(SettingsManager.Load().MenuTaxonomy));
        if (!map.TryGetValue(category, out var subCategories))
        {
            if (LegacyCategoryMap.TryGetValue(category, out var legacy))
                subCategories = legacy;
            else
                subCategories = new List<string> { category };
        }

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

    protected override void RefreshLocalizedStrings()
    {
        base.RefreshLocalizedStrings();
        InitializeViewCategories();
        Notify(
            nameof(PageTitle),
            nameof(PageSubtitle),
            nameof(AddProductLabel),
            nameof(MenuTypeLabel),
            nameof(SubmenuTypeLabel),
            nameof(SearchTooltip),
            nameof(DetailsLabel),
            nameof(EditLabel),
            nameof(DeleteLabel),
            nameof(MenuFieldProductNameLabel),
            nameof(MenuFieldSectionLabel),
            nameof(MenuFieldTypeLabel),
            nameof(MenuFieldPriceLabel),
            nameof(MenuFieldCookingTimeLabel),
            nameof(MenuFieldCookingTimeHelp),
            nameof(MenuFieldDescriptionLabel),
            nameof(MenuFieldCompositionLabel),
            nameof(MenuDetailsDescriptionLabel),
            nameof(MenuDetailsCompositionLabel),
            nameof(MenuFieldProductImageLabel),
            nameof(BrowseLabel),
            nameof(ClearLabel),
            nameof(MenuNoImageSelectedLabel),
            nameof(MenuInventoryIngredientsLabel),
            nameof(MenuStockPrefix),
            nameof(MenuSaveProductLabel),
            nameof(CancelLabel),
            nameof(CloseLabel),
            nameof(MenuNoImageAvailableLabel),
            nameof(MenuFieldProductIdLabel),
            nameof(MenuFieldCookingTimeShortLabel),
            nameof(MenuIngredientsLabel),
            nameof(MenuIngredientsPanelTitle),
            nameof(MenuIngredientsPanelHint),
            nameof(MenuIngredientSearchPlaceholder),
            nameof(MenuEditorDialogSubtitle),
            nameof(MenuIngredientQtyLabel),
            nameof(SelectedIngredientsCountText),
            nameof(SelectedIngredientsSummaryText),
            nameof(DetailsIngredientsPanelSubtitle),
            nameof(DescriptionCharCountRemainingText));

        if (IsDialogOpen && IsDetailsDialogMode)
        {
            DialogTitle = Loc.Admin("menuDetailsTitle", "Product Details");
            if (_detailsProduct is not null)
                ApplyDetailsDisplayFields(_detailsProduct);
        }
        else if (IsDialogOpen && !IsDetailsDialogMode)
        {
            DialogTitle = _editingProductId.HasValue
                ? Loc.Admin("menuEditDialog", "Edit Product")
                : Loc.Admin("menuAddDialog", "Add Product");
        }

        RefreshGroupedProducts();
    }
}
