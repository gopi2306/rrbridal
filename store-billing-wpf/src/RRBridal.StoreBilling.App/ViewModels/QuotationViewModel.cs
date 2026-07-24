using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using RRBridal.StoreBilling.App.Services;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class QuotationViewModel : ObservableObject
{
    private readonly AppServices _services;

    public BillingViewModel Editor { get; }

    public Action? NavigateToQuotationList { get; set; }

    [ObservableProperty] private string _quotationNo = "—";
    [ObservableProperty] private string _statusMessage = "Create a quotation like a bill, then save. Convert from Quotation list when ready.";

    public QuotationViewModel(AppServices services)
    {
        _services = services;
        Editor = new BillingViewModel(services);
        Editor.OnlineCodOrder = false;
    }

    public void LoadDocument(BsonDocument doc)
    {
        Editor.LoadQuotationForEdit(doc);
        QuotationNo = doc.GetValue("quotationNo", "—").AsString;
        Editor.ActiveEditQuotationNo = QuotationNo == "—" ? null : QuotationNo;
        StatusMessage = $"Editing {QuotationNo}.";
    }

    public void StartNew()
    {
        Editor.ClearForNewBillCommand.Execute(null);
        Editor.OnlineCodOrder = false;
        Editor.ActiveEditQuotationNo = null;
        QuotationNo = "—";
        StatusMessage = "New quotation.";
    }

    [RelayCommand]
    private async Task SaveQuotation()
    {
        if (!Editor.IsCustomerReadyForPost)
        {
            AppDialog.Show(
                "Enter customer name and a valid 10-digit mobile number before saving the quotation.",
                "Quotation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Editor.Lines.Any(l => l.Amount > 0))
        {
            AppDialog.Show(
                "Add at least one line with quantity × rate before saving.",
                "Quotation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var payload = Editor.ExportPayloadDocument();
            var existing = Editor.ActiveEditQuotationNo;
            var savedNo = await _services.Quotations.UpsertAsync(payload, existing);
            QuotationNo = savedNo;
            Editor.ActiveEditQuotationNo = savedNo;
            StatusMessage = $"Quotation {savedNo} saved.";
            AppDialog.Show($"Quotation {savedNo} saved.", "Quotation", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not save quotation: {ex.Message}", "Quotation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PrintQuotation()
    {
        if (!Editor.Lines.Any(l => l.Amount > 0))
        {
            AppDialog.Show("Add at least one line before printing.", "Quotation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(Editor.ActiveEditQuotationNo))
                await SaveQuotation();

            var draftBillNo = string.IsNullOrWhiteSpace(QuotationNo) || QuotationNo == "—"
                ? "QUOT-DRAFT"
                : QuotationNo;

            Editor.BillNo = draftBillNo;
            await Editor.PrintStubCommand.ExecuteAsync(null);
            Editor.BillNo = "—";
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Could not print quotation: {ex.Message}", "Quotation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void NewQuotation() => StartNew();

    [RelayCommand]
    private void BackToList() => NavigateToQuotationList?.Invoke();
}
