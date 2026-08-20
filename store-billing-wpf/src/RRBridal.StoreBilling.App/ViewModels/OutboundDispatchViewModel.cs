using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Billing;
using RRBridal.StoreBilling.App.Services.Customers;
using RRBridal.StoreBilling.App.Services.Dispatch;
using RRBridal.StoreBilling.App.Services.Expenses;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class OutboundDispatchRow : ObservableObject
{
    public required BsonDocument Document { get; set; }
    public required string DispatchNo { get; init; }
    public required string BillNo { get; init; }
    public string BusinessDate { get; init; } = "";
    public string Status { get; init; } = "";
    public string BatchNo { get; init; } = "";
    public string Customer { get; init; } = "";
    public string Carrier { get; init; } = "";
    public string TrackingNo { get; init; } = "";
    public string FeePayer { get; init; } = "";
    public decimal Fee { get; init; }
    public string FeeDisplay => Fee.ToString("C2", CultureInfo.GetCultureInfo("en-IN"));
    [ObservableProperty] private bool _isBatchSelected;
}

public partial class OutboundDispatchViewModel : ObservableObject
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");
    private readonly AppServices _services;
    private readonly CustomerLookupService _customerLookup;
    private BsonDocument? _loadedBill;
    private BsonDocument? _loadedDispatch;

    [ObservableProperty] private string _billSearch = "";
    [ObservableProperty] private BillSearchRow? _selectedBill;
    [ObservableProperty] private string _loadedBillNo = "";
    [ObservableProperty] private string _dispatchNo = "";
    [ObservableProperty] private string _batchNo = "";
    [ObservableProperty] private string _currentStatus = "New";
    [ObservableProperty] private string _shipToName = "";
    [ObservableProperty] private string _shipToPhone = "";
    [ObservableProperty] private string _addressLine1 = "";
    [ObservableProperty] private string _addressLine2 = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _state = "";
    [ObservableProperty] private string _pincode = "";
    [ObservableProperty] private string _landmark = "";
    [ObservableProperty] private string _addressSource = "";
    [ObservableProperty] private string _carrierType = DispatchCarrierType.Courier;
    [ObservableProperty] private string _carrierName = "";
    [ObservableProperty] private string _trackingNo = "";
    [ObservableProperty] private int _packageCount = 1;
    [ObservableProperty] private string _feePayer = DispatchFeePayer.Store;
    [ObservableProperty] private string _feeText = "0.00";
    [ObservableProperty] private string _feePaymentMode = ExpensePaymentMode.Cash;
    [ObservableProperty] private string _feePaymentReference = "";
    [ObservableProperty] private string _statusReason = "";
    [ObservableProperty] private DateTime? _filterBusinessDate = DateTime.Today;
    [ObservableProperty] private string _filterStatus = "All";
    [ObservableProperty] private string _filterBatchNo = "";
    [ObservableProperty] private string _filterSearch = "";
    [ObservableProperty] private OutboundDispatchRow? _selectedDispatch;
    [ObservableProperty] private string _statusMessage = "Search a posted bill or select a dispatch.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _batchCustomerTotal = "₹ 0.00";
    [ObservableProperty] private string _batchStoreTotal = "₹ 0.00";
    [ObservableProperty] private string _batchGrandTotal = "₹ 0.00";

    public ObservableCollection<BillSearchRow> BillResults { get; } = [];
    public ObservableCollection<OutboundDispatchRow> Dispatches { get; } = [];
    public IReadOnlyList<string> CarrierTypes { get; } = [DispatchCarrierType.Courier, DispatchCarrierType.Post];
    public IReadOnlyList<string> FeePayers { get; } = [DispatchFeePayer.Customer, DispatchFeePayer.Store];
    public IReadOnlyList<string> PaymentModes { get; } = ExpensePaymentMode.All;
    public IReadOnlyList<string> StatusFilters { get; } =
        ["All", OutboundDispatchStatus.Draft, OutboundDispatchStatus.Ready,
            OutboundDispatchStatus.HandedOver, OutboundDispatchStatus.Delivered,
            OutboundDispatchStatus.Cancelled, OutboundDispatchStatus.Returned];

    public bool HasLoadedBill => _loadedBill != null;
    public bool HasDispatch => _loadedDispatch != null;
    public bool CanEditDraft => _loadedDispatch == null
                                || string.Equals(CurrentStatus, OutboundDispatchStatus.Draft, StringComparison.Ordinal);
    public bool CanPrintChargeReceipt => _loadedDispatch?.Contains("chargeReceiptNo") == true
                                         || (_loadedDispatch?.TryGetValue("chargeReceipt", out var receipt) == true
                                             && receipt.IsBsonDocument);

    public OutboundDispatchViewModel(AppServices services)
    {
        _services = services;
        _customerLookup = new CustomerLookupService(services.LocalDb, services.CentralApi, services.CentralMode);
        _ = RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SearchBills()
    {
        await RunBusyAsync(async () =>
        {
            BillResults.Clear();
            var rows = await _services.BillDocuments.SearchBillsAsync(
                string.IsNullOrWhiteSpace(BillSearch) ? null : BillSearch.Trim(),
                null, null, null, null, status: "posted", limit: 200);
            foreach (var row in rows) BillResults.Add(row);
            SelectedBill = BillResults.FirstOrDefault();
            StatusMessage = rows.Count == 0
                ? "No posted bills found."
                : $"{rows.Count} posted bill(s) found. Select one and load it.";
        });
    }

    [RelayCommand]
    private async Task LoadSelectedBill()
    {
        if (SelectedBill == null)
        {
            ShowInfo("Select a posted bill first.");
            return;
        }
        await OpenBillAsync(SelectedBill.BillNo);
    }

    public async Task OpenBillAsync(string billNo)
    {
        await RunBusyAsync(async () =>
        {
            var bill = await _services.BillDocuments.GetByBillNoAsync(billNo);
            if (bill == null)
                throw new InvalidOperationException($"Bill {billNo} was not found.");
            OutboundDispatchDocumentMapper.ValidatePostedBill(bill);
            _loadedBill = bill;
            LoadedBillNo = OutboundDispatchDocumentMapper.ReadString(bill, "billNo") ?? billNo;

            var active = await _services.OutboundDispatches.GetActiveByBillNoAsync(LoadedBillNo);
            if (active != null)
            {
                LoadDispatch(active);
                StatusMessage = $"Opened active dispatch {DispatchNo} for bill {LoadedBillNo}.";
                return;
            }

            _loadedDispatch = null;
            DispatchNo = "";
            BatchNo = "";
            CurrentStatus = "New";
            var customer = await ResolveCustomerMasterAsync(bill);
            ApplyAddress(DispatchAddressResolver.Resolve(bill, customer));
            ResetDispatchFields();
            NotifyEditorState();
            StatusMessage = $"Loaded posted bill {LoadedBillNo}. Review ship-to details and create a draft.";
        });
    }

    [RelayCommand]
    private async Task SaveDraft()
    {
        if (_loadedBill == null)
        {
            ShowInfo("Load a posted bill first.");
            return;
        }
        await RunBusyAsync(async () =>
        {
            var draft = BuildDraft();
            if (_loadedDispatch == null)
                _loadedDispatch = await _services.OutboundDispatches.CreateAsync(
                    _loadedBill, draft, _services.UserSession);
            else
                _loadedDispatch = await _services.OutboundDispatches.UpdateDraftAsync(
                    _loadedDispatch, draft, _services.UserSession);
            LoadDispatch(_loadedDispatch);
            StatusMessage = $"Draft {DispatchNo} saved.";
            await RefreshCoreAsync(selectDispatchNo: DispatchNo);
        });
    }

    [RelayCommand]
    private async Task ChangeStatus(string? nextStatus)
    {
        if (_loadedDispatch == null || string.IsNullOrWhiteSpace(nextStatus))
        {
            ShowInfo("Open or create a dispatch first.");
            return;
        }
        if (nextStatus is OutboundDispatchStatus.Cancelled or OutboundDispatchStatus.Returned
            && string.IsNullOrWhiteSpace(StatusReason))
        {
            ShowInfo($"{(nextStatus == OutboundDispatchStatus.Cancelled ? "Cancellation" : "Return")} reason is required.");
            return;
        }
        if (AppDialog.Show(
                $"Change dispatch {DispatchNo} from {CurrentStatus} to {nextStatus}?",
                "Outbound Dispatch", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        await RunBusyAsync(async () =>
        {
            _loadedDispatch = await _services.OutboundDispatches.TransitionAsync(
                _loadedDispatch, nextStatus, _services.UserSession, StatusReason);
            LoadDispatch(_loadedDispatch);
            StatusMessage = $"Dispatch {DispatchNo} changed to {CurrentStatus}.";
            StatusReason = "";
            await RefreshCoreAsync(selectDispatchNo: DispatchNo);
        });
    }

    [RelayCommand]
    private async Task Refresh() => await RunBusyAsync(() => RefreshCoreAsync());

    private async Task RefreshCoreAsync(string? selectDispatchNo = null)
    {
        var query = new OutboundDispatchQuery
        {
            BusinessDate = FilterBusinessDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Status = string.Equals(FilterStatus, "All", StringComparison.OrdinalIgnoreCase) ? null : FilterStatus,
            BatchNo = string.IsNullOrWhiteSpace(FilterBatchNo) ? null : FilterBatchNo.Trim(),
            Search = string.IsNullOrWhiteSpace(FilterSearch) ? null : FilterSearch.Trim(),
            Limit = 500,
        };
        var docs = await _services.OutboundDispatches.ListAsync(query);
        Dispatches.Clear();
        foreach (var doc in docs)
        {
            var row = ToRow(doc);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(OutboundDispatchRow.IsBatchSelected))
                    RecalculateBatchTotals();
            };
            Dispatches.Add(row);
        }
        SelectedDispatch = Dispatches.FirstOrDefault(r =>
                               string.Equals(r.DispatchNo, selectDispatchNo, StringComparison.OrdinalIgnoreCase))
                           ?? Dispatches.FirstOrDefault();
        RecalculateBatchTotals();
        StatusMessage = Dispatches.Count == 0
            ? "No dispatches match the current filters."
            : $"{Dispatches.Count} dispatch(es) shown.";
    }

    [RelayCommand]
    private void OpenSelectedDispatch()
    {
        if (SelectedDispatch == null)
        {
            ShowInfo("Select a dispatch first.");
            return;
        }
        LoadDispatch(SelectedDispatch.Document);
        _ = LoadBillForDispatchAsync(SelectedDispatch.BillNo);
        StatusMessage = $"Opened dispatch {DispatchNo}.";
    }

    [RelayCommand]
    private async Task AssignBatch()
    {
        var selected = Dispatches.Where(r => r.IsBatchSelected).ToList();
        await RunBusyAsync(async () =>
        {
            var updated = await _services.OutboundDispatches.AssignCommonBatchAsync(
                selected.Select(r => r.Document), _services.UserSession);
            var batch = OutboundDispatchDocumentMapper.ReadString(updated[0], "batchNo") ?? "";
            FilterBatchNo = batch;
            StatusMessage = $"Assigned {updated.Count} dispatches to batch {batch}.";
            await RefreshCoreAsync();
        });
    }

    [RelayCommand] private Task PrintParcelA4() => PrintSelectedAsync(d => OutboundDispatchPrintFlow.PreviewParcelA4Async(_services, d));
    [RelayCommand] private Task PrintParcelThermal() => PrintSelectedAsync(d => OutboundDispatchPrintFlow.PreviewParcelThermalAsync(_services, d));
    [RelayCommand] private Task PrintChargeA4() => PrintSelectedAsync(d => OutboundDispatchPrintFlow.PreviewChargeReceiptA4Async(_services, d), requireCharge: true);
    [RelayCommand] private Task PrintChargeThermal() => PrintSelectedAsync(d => OutboundDispatchPrintFlow.PreviewChargeReceiptThermalAsync(_services, d), requireCharge: true);
    [RelayCommand] private Task PrintBatchA4() => PrintBatchAsync(d => OutboundDispatchPrintFlow.PreviewBatchA4Async(_services, d));
    [RelayCommand] private Task PrintBatchThermal() => PrintBatchAsync(d => OutboundDispatchPrintFlow.PreviewBatchThermalAsync(_services, d));

    private async Task PrintSelectedAsync(Func<BsonDocument, Task<bool>> print, bool requireCharge = false)
    {
        var doc = _loadedDispatch ?? SelectedDispatch?.Document;
        if (doc == null)
        {
            ShowInfo("Open a dispatch first.");
            return;
        }
        if (requireCharge && !HasChargeReceipt(doc))
        {
            ShowInfo("A customer charge receipt is available only after a customer-paid fee is posted at Ready.");
            return;
        }
        await print(doc);
    }

    private async Task PrintBatchAsync(Func<IEnumerable<BsonDocument>, Task<bool>> print)
    {
        var docs = Dispatches.Where(r => r.IsBatchSelected).Select(r => r.Document).ToList();
        if (docs.Count == 0 && SelectedDispatch != null && !string.IsNullOrWhiteSpace(SelectedDispatch.BatchNo))
            docs = Dispatches.Where(r => string.Equals(r.BatchNo, SelectedDispatch.BatchNo, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Document).ToList();
        if (docs.Count == 0)
        {
            ShowInfo("Tick dispatches in the list, or select a dispatch whose full batch is visible.");
            return;
        }
        await print(docs);
    }

    private async Task LoadBillForDispatchAsync(string billNo)
    {
        _loadedBill = await _services.BillDocuments.GetByBillNoAsync(billNo);
        NotifyEditorState();
    }

    private async Task<BsonDocument?> ResolveCustomerMasterAsync(BsonDocument bill)
    {
        var code = OutboundDispatchDocumentMapper.ReadString(bill, "customerCode") ?? "";
        var phone = OutboundDispatchDocumentMapper.ReadString(bill, "customerPhone") ?? "";
        var query = !string.IsNullOrWhiteSpace(code) ? code : phone;
        if (string.IsNullOrWhiteSpace(query)) return null;
        var matches = await _customerLookup.SearchAsync(query);
        var match = matches.FirstOrDefault(m =>
                        !string.IsNullOrWhiteSpace(code)
                        && string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase))
                    ?? matches.FirstOrDefault(m =>
                        !string.IsNullOrWhiteSpace(phone)
                        && string.Equals(Digits(m.Phone), Digits(phone), StringComparison.Ordinal))
                    ?? matches.FirstOrDefault();
        return match == null ? null : new BsonDocument
        {
            { "name", match.Name }, { "phone", match.Phone },
            { "addressLine1", string.IsNullOrWhiteSpace(match.FullAddress)
                ? string.Join(" ", new[] { match.DoorNo, match.Street }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : match.FullAddress },
            { "city", match.City }, { "state", match.State }, { "pincode", match.Pincode },
        };
    }

    private OutboundDispatchDraft BuildDraft()
    {
        if (!TryMoney(FeeText, out var fee))
            throw new InvalidOperationException("Enter a valid dispatch fee.");
        var draft = new OutboundDispatchDraft
        {
            ShipTo = new DispatchAddress(ShipToName, ShipToPhone, AddressLine1, AddressLine2,
                City, State, Pincode, Landmark, "manual"),
            CarrierType = CarrierType,
            CarrierName = CarrierName,
            TrackingNo = TrackingNo,
            PackageCount = PackageCount,
            FeePayer = FeePayer,
            Fee = fee,
            FeePaymentMode = FeePaymentMode,
            FeePaymentReference = FeePaymentReference,
            BusinessDate = FilterBusinessDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
        };
        OutboundDispatchDocumentMapper.ValidateDraft(draft);
        return draft;
    }

    private void LoadDispatch(BsonDocument doc)
    {
        _loadedDispatch = doc;
        DispatchNo = Read(doc, "dispatchNo");
        BatchNo = Read(doc, "batchNo");
        LoadedBillNo = Read(doc, "billNo");
        CurrentStatus = Read(doc, "status");
        CarrierType = Read(doc, "carrierType", DispatchCarrierType.Courier);
        CarrierName = Read(doc, "carrierName");
        TrackingNo = Read(doc, "trackingNo");
        PackageCount = Math.Max(1, (int)OutboundDispatchDocumentMapper.ReadDecimal(doc, "packageCount"));
        FeePayer = Read(doc, "feePayer", DispatchFeePayer.Store);
        FeeText = OutboundDispatchDocumentMapper.ReadDecimal(doc, "dispatchFee").ToString("0.00", InCulture);
        FeePaymentMode = Read(doc, "feePaymentMode", ExpensePaymentMode.Cash);
        FeePaymentReference = Read(doc, "feePaymentReference");
        if (doc.TryGetValue("shipTo", out var value) && value.IsBsonDocument)
        {
            var a = value.AsBsonDocument;
            ApplyAddress(new DispatchAddress(Read(a, "name"), Read(a, "phone"), Read(a, "addressLine1"),
                Read(a, "addressLine2"), Read(a, "city"), Read(a, "state"), Read(a, "pincode"),
                Read(a, "landmark"), Read(a, "source")));
        }
        NotifyEditorState();
    }

    private void ResetDispatchFields()
    {
        CarrierType = DispatchCarrierType.Courier;
        CarrierName = "";
        TrackingNo = "";
        PackageCount = 1;
        FeePayer = DispatchFeePayer.Store;
        FeeText = "0.00";
        FeePaymentMode = ExpensePaymentMode.Cash;
        FeePaymentReference = "";
        StatusReason = "";
    }

    private void ApplyAddress(DispatchAddress address)
    {
        ShipToName = address.Name;
        ShipToPhone = address.Phone;
        AddressLine1 = address.AddressLine1;
        AddressLine2 = address.AddressLine2;
        City = address.City;
        State = address.State;
        Pincode = address.Pincode;
        Landmark = address.Landmark;
        AddressSource = address.Source;
    }

    private void RecalculateBatchTotals()
    {
        var selected = Dispatches.Where(r => r.IsBatchSelected).ToList();
        if (selected.Count == 0 && !string.IsNullOrWhiteSpace(FilterBatchNo))
            selected = Dispatches.Where(r =>
                string.Equals(r.BatchNo, FilterBatchNo.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (selected.Count == 0 && !string.IsNullOrWhiteSpace(SelectedDispatch?.BatchNo))
            selected = Dispatches.Where(r =>
                string.Equals(r.BatchNo, SelectedDispatch.BatchNo, StringComparison.OrdinalIgnoreCase)).ToList();
        BatchCustomerTotal = Money(selected.Where(r => r.FeePayer == DispatchFeePayer.Customer).Sum(r => r.Fee));
        BatchStoreTotal = Money(selected.Where(r => r.FeePayer == DispatchFeePayer.Store).Sum(r => r.Fee));
        BatchGrandTotal = Money(selected.Sum(r => r.Fee));
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AppDialog.Show(ex.Message, "Outbound Dispatch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsBusy = false; }
    }

    private void NotifyEditorState()
    {
        OnPropertyChanged(nameof(HasLoadedBill));
        OnPropertyChanged(nameof(HasDispatch));
        OnPropertyChanged(nameof(CanEditDraft));
        OnPropertyChanged(nameof(CanPrintChargeReceipt));
    }

    private static OutboundDispatchRow ToRow(BsonDocument doc)
    {
        var snapshot = doc.TryGetValue("billSnapshot", out var value) && value.IsBsonDocument
            ? value.AsBsonDocument : new BsonDocument();
        var customer = snapshot.TryGetValue("customer", out value) && value.IsBsonDocument
            ? value.AsBsonDocument : new BsonDocument();
        return new OutboundDispatchRow
        {
            Document = doc,
            DispatchNo = Read(doc, "dispatchNo"),
            BillNo = Read(doc, "billNo"),
            BusinessDate = Read(doc, "businessDate"),
            Status = Read(doc, "status"),
            BatchNo = Read(doc, "batchNo"),
            Customer = Read(customer, "name"),
            Carrier = Read(doc, "carrierName"),
            TrackingNo = Read(doc, "trackingNo"),
            FeePayer = Read(doc, "feePayer"),
            Fee = OutboundDispatchDocumentMapper.ReadDecimal(doc, "dispatchFee"),
        };
    }

    private static bool HasChargeReceipt(BsonDocument doc) =>
        !string.IsNullOrWhiteSpace(Read(doc, "chargeReceiptNo"))
        || doc.TryGetValue("chargeReceipt", out var value) && value.IsBsonDocument;
    private static string Read(BsonDocument doc, string field, string fallback = "") =>
        OutboundDispatchDocumentMapper.ReadString(doc, field) ?? fallback;
    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string Money(decimal value) => value.ToString("C2", InCulture);
    private static bool TryMoney(string? text, out decimal value) =>
        decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, InCulture, out value)
        || decimal.TryParse((text ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    private static void ShowInfo(string message) =>
        AppDialog.Show(message, "Outbound Dispatch", MessageBoxButton.OK, MessageBoxImage.Information);

    partial void OnSelectedDispatchChanged(OutboundDispatchRow? value) => RecalculateBatchTotals();
    partial void OnFilterBatchNoChanged(string value) => RecalculateBatchTotals();
}
