using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EliteRestaurant.Contracts.Clients;
using EliteRestaurantPro.ApiClients;
using EliteRestaurantPro.Localization;
using EliteRestaurantPro.ViewModels;

namespace EliteRestaurantPro.Views;

public sealed class ClientPickerRowViewModel
{
    public RestaurantClientSearchResultDto Source { get; }
    public int Id => Source.Id;
    public string FullName => Source.FullName;
    public string UniqueId => Source.UniqueId;
    public string PrimaryPhone => Source.PrimaryPhone;
    public string MetaLine =>
        string.IsNullOrWhiteSpace(PrimaryPhone)
            ? UniqueId
            : $"{PrimaryPhone} · {UniqueId}";

    public ClientPickerRowViewModel(RestaurantClientSearchResultDto source) => Source = source;

    public static ClientPickerRowViewModel FromDto(RestaurantClientSearchResultDto dto) => new(dto);

    public static ClientPickerRowViewModel FromListItem(RestaurantClientListItemDto dto) =>
        new(new RestaurantClientSearchResultDto(
            dto.Id,
            dto.UniqueId,
            dto.FullName,
            dto.PrimaryPhone,
            dto.IsStaffClient,
            dto.DebtBalanceUsd));
}

public sealed class ClientPickerDialogViewModel : BaseViewModel, IDisposable
{
    private static readonly Regex PhoneDigits = new(@"\D", RegexOptions.Compiled);

    private readonly ClientsApiClient _clientsApi = new();
    private readonly DispatcherTimer _searchDebounce;
    private readonly List<ClientPickerRowViewModel> _allClients = new();
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private ClientPickerRowViewModel? _selectedRow;
    private bool _isLoading;
    private bool _hasLoaded;

    public string DialogTitle { get; }
    public string SearchPlaceholder { get; }
    public string SelectButtonLabel { get; }
    public string CancelButtonLabel { get; }

    public ObservableCollection<ClientPickerRowViewModel> Results { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value))
                return;
            if (_hasLoaded)
                RestartSearchDebounce();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ClientPickerRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetField(ref _selectedRow, value))
                return;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public bool CanConfirm => SelectedRow is not null && !_isLoading;

    public ClientPickerDialogViewModel(string initialSearch)
    {
        DialogTitle = Loc.Admin("createOrderClientPickerTitle", "Link client");
        SearchPlaceholder = Loc.Admin("createOrderClientPickerSearch", "Search by name, phone, or ID…");
        SelectButtonLabel = Loc.Admin("createOrderClientPickerSelect", "Select");
        CancelButtonLabel = Loc.Admin("createOrderClientPickerCancel", "Cancel");

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            ApplyFilter();
        };

        _searchText = initialSearch ?? string.Empty;
        StatusMessage = Loc.Admin("createOrderClientPickerLoading", "Loading clients…");
    }

    public void RestartSearchDebounce()
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    public async Task LoadInitialAsync()
    {
        if (_hasLoaded)
            return;

        _isLoading = true;
        OnPropertyChanged(nameof(CanConfirm));
        StatusMessage = Loc.Admin("createOrderClientPickerLoading", "Loading clients…");

        try
        {
            var rows = await _clientsApi.ListAsync().ConfigureAwait(true);
            _allClients.Clear();
            if (rows is not null)
            {
                foreach (var row in rows)
                    _allClients.Add(ClientPickerRowViewModel.FromListItem(row));
            }

            _hasLoaded = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            _isLoading = false;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        var phoneNeedle = PhoneDigits.Replace(q, string.Empty);

        Results.Clear();
        SelectedRow = null;

        IEnumerable<ClientPickerRowViewModel> filtered = _allClients;
        if (!string.IsNullOrEmpty(q))
        {
            filtered = _allClients.Where(c =>
                c.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || c.UniqueId.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(phoneNeedle)
                    && c.PrimaryPhone.Contains(phoneNeedle, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var row in filtered)
            Results.Add(row);

        if (Results.Count == 0)
        {
            StatusMessage = Loc.Admin("createOrderClientPickerEmpty", "No clients found.");
        }
        else if (string.IsNullOrEmpty(q))
        {
            StatusMessage = Loc.Admin(
                "createOrderClientPickerShowingAll",
                "{{count}} clients — type to filter.",
                new Dictionary<string, string> { ["count"] = Results.Count.ToString() });
        }
        else
        {
            StatusMessage = string.Empty;
        }

        if (Results.Count == 1)
            SelectedRow = Results[0];

        OnPropertyChanged(nameof(CanConfirm));
    }

    public void Dispose() => _searchDebounce.Stop();
}

public partial class ClientPickerDialog : Window
{
    private readonly ClientPickerDialogViewModel _viewModel;

    public RestaurantClientSearchResultDto? SelectedClient { get; private set; }

    public ClientPickerDialog(string initialSearch)
    {
        _viewModel = new ClientPickerDialogViewModel(initialSearch);
        DataContext = _viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            await _viewModel.LoadInitialAsync();
        };
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // HWND not ready — ignore.
            }
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

    private void ConfirmSelection()
    {
        if (_viewModel.SelectedRow is null)
            return;

        SelectedClient = _viewModel.SelectedRow.Source;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
