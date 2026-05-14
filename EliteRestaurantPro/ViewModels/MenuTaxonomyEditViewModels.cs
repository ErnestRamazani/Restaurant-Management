using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EliteRestaurantPro.ViewModels;

public sealed class MenuTaxonomySectionEditVm : BaseViewModel
{
    private readonly AppearanceSettingsViewModel _owner;
    private readonly MenuTaxonomyTypeEditVm _typeVm;
    private string _name = string.Empty;
    private string _itemsText = string.Empty;

    public MenuTaxonomySectionEditVm(AppearanceSettingsViewModel owner, MenuTaxonomyTypeEditVm typeVm)
    {
        _owner = owner;
        _typeVm = typeVm;
        RemoveCommand = new RelayCommand(_ => _owner.RemoveMenuTaxonomySection(_typeVm, this));
    }

    public ICommand RemoveCommand { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>Comma-separated subcategories (Product.SubCategory).</summary>
    public string ItemsText
    {
        get => _itemsText;
        set => SetField(ref _itemsText, value);
    }
}

public sealed class MenuTaxonomyTypeEditVm : BaseViewModel
{
    private readonly AppearanceSettingsViewModel _owner;
    private string _name = string.Empty;
    private bool _isDrink;

    public MenuTaxonomyTypeEditVm(AppearanceSettingsViewModel owner)
    {
        _owner = owner;
        RemoveTypeCommand = new RelayCommand(_ => _owner.RemoveMenuTaxonomyType(this));
        AddSectionCommand = new RelayCommand(_ => _owner.AddMenuTaxonomySection(this));
    }

    public ObservableCollection<MenuTaxonomySectionEditVm> Sections { get; } = new();

    public ICommand RemoveTypeCommand { get; }

    public ICommand AddSectionCommand { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>When true, products use drink matching (Category Drink + sections, or explicit section categories).</summary>
    public bool IsDrink
    {
        get => _isDrink;
        set => SetField(ref _isDrink, value);
    }
}
