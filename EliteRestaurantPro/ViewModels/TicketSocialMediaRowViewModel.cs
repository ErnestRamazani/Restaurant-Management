using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EliteRestaurantPro.ViewModels;

/// <summary>One editable social line for printed/PDF tickets (Appearance → Tickets &amp; receipts).</summary>
public sealed class TicketSocialMediaRowViewModel : INotifyPropertyChanged
{
    private string _platformName = string.Empty;
    private string _userText = string.Empty;
    private string _iconPath = string.Empty;

    public TicketSocialMediaRowViewModel()
    {
    }

    public TicketSocialMediaRowViewModel(string platformName, string userText, string iconPath)
    {
        _platformName = platformName;
        _userText = userText;
        _iconPath = iconPath;
    }

    public string PlatformName
    {
        get => _platformName;
        set
        {
            if (_platformName == value)
                return;
            _platformName = value;
            OnPropertyChanged();
        }
    }

    public string UserText
    {
        get => _userText;
        set
        {
            if (_userText == value)
                return;
            _userText = value;
            OnPropertyChanged();
        }
    }

    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath == value)
                return;
            _iconPath = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
