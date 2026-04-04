namespace EliteRestaurantPro.ViewModels;

public class MainViewModel : BaseViewModel
{
    private BaseViewModel _currentViewModel = null!;

    public BaseViewModel CurrentViewModel
    {
        get => _currentViewModel;
        set => SetField(ref _currentViewModel, value);
    }

    public MainViewModel()
    {
        Navigate(new RoleSelectionViewModel(Navigate));
    }

    public void Navigate(BaseViewModel viewModel)
    {
        CurrentViewModel = viewModel;
    }
}
