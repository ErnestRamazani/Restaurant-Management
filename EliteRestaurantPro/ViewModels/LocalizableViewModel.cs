using System.Windows;
using EliteRestaurantPro.Localization;

namespace EliteRestaurantPro.ViewModels;

public abstract class LocalizableViewModel : BaseViewModel
{
    protected LocalizableViewModel()
    {
        Loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
            RefreshLocalizedStrings();
        else
            Application.Current?.Dispatcher.Invoke(RefreshLocalizedStrings);
    }

    protected virtual void RefreshLocalizedStrings() { }

    protected void Notify(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            OnPropertyChanged(name);
    }
}
