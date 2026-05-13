using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Store;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class LedgerViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private const int MaxBills = 200;
    private const int MaxPayments = 200;

    private readonly StoreLedgerService _ledgerService;
    private readonly StoreContext _storeContext;

    [ObservableProperty] private string _storeIdDisplay = "";

    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<LedgerBillRow> Bills { get; } = new();

    public ObservableCollection<LedgerPaymentRow> Payments { get; } = new();

    public LedgerViewModel(AppServices services)
    {
        _ledgerService = new StoreLedgerService(services.LocalDb);
        _storeContext = services.StoreContext;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        StatusMessage = "Loading ledger…";
        try
        {
            var storeId = _storeContext.StoreId;
            StoreIdDisplay = storeId;

            var snap = await _ledgerService.LoadAsync(storeId, MaxBills, MaxPayments);

            Bills.Clear();
            foreach (var b in snap.Bills)
                Bills.Add(b);

            Payments.Clear();
            foreach (var p in snap.Payments)
                Payments.Add(p);

            StatusMessage = $"Loaded {Bills.Count} bill(s), {Payments.Count} payment(s). Updated {DateTime.Now.ToString("T", InCulture)}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not load ledger: " + ex.Message;
        }
    }
}
