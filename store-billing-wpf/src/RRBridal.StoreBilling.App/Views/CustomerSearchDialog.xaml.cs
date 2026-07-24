using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Views;

public partial class CustomerSearchDialog : Window, INotifyPropertyChanged
{
    private readonly CustomerLookupService _lookupService;

    private string _searchQuery = "";
    private string _statusText = "";
    private CustomerMatch? _selectedItem;

    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; OnPropChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropChanged(); }
    }

    public CustomerMatch? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; OnPropChanged(); }
    }

    public ObservableCollection<CustomerMatch> Results { get; } = new();

    public CustomerMatch? SelectedCustomer { get; private set; }
    public bool WantsNewRegistration { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomerSearchDialog(string initialQuery, CustomerLookupService lookupService)
    {
        _lookupService = lookupService;
        InitializeComponent();
        DataContext = this;
        SearchQuery = initialQuery;

        Loaded += async (_, _) =>
        {
            DialogLayoutHelper.CenterAndClamp(this, Owner);
            SearchBox.Focus();
            SearchBox.SelectAll();
            if (!string.IsNullOrWhiteSpace(initialQuery))
                await RunSearchAsync();
        };
    }

    private async Task RunSearchAsync()
    {
        var q = SearchQuery.Trim();
        if (string.IsNullOrEmpty(q))
        {
            StatusText = "Enter a search term.";
            return;
        }

        StatusText = "Searching…";
        Results.Clear();

        var matches = await _lookupService.SearchAsync(q);

        foreach (var m in matches)
            Results.Add(m);

        StatusText = matches.Count == 0
            ? "No customers found. You can register a new customer."
            : $"{matches.Count} customer(s) found.";
    }

    private async void Search_OnClick(object sender, RoutedEventArgs e) => await RunSearchAsync();

    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = RunSearchAsync();
            e.Handled = true;
        }
    }

    private void Results_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedItem != null)
        {
            SelectedCustomer = SelectedItem;
            DialogResult = true;
        }
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
        {
            AppDialog.Show("Select a customer from the list.", "Find Customer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedCustomer = SelectedItem;
        DialogResult = true;
    }

    private void RegisterNew_OnClick(object sender, RoutedEventArgs e)
    {
        WantsNewRegistration = true;
        DialogResult = false;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnPropChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
