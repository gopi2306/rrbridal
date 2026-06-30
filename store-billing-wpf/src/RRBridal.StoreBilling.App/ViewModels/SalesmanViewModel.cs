using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Salesmen;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class SalesmanViewModel : ObservableObject
{
    private readonly SalesmanService _salesmanService;
    private readonly SalesmanCodeGenerator _codeGenerator;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editCode = "";
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _editPanelTitle = "New salesman";
    [ObservableProperty] private SalesmanRecord? _selectedSalesman;

    public ObservableCollection<SalesmanRecord> Salesmen { get; } = new();

    public bool ShowEditIsActive => SelectedSalesman != null;

    public SalesmanViewModel(AppServices services)
    {
        _salesmanService = new SalesmanService(services.LocalDb, services.CentralApi, services.StoreContext);
        _codeGenerator = new SalesmanCodeGenerator(services.LocalDb);
    }

    partial void OnSelectedSalesmanChanged(SalesmanRecord? value)
    {
        if (value == null)
        {
            ClearEditForm();
            IsEditing = false;
            EditPanelTitle = "New salesman";
            OnPropertyChanged(nameof(ShowEditIsActive));
            return;
        }

        EditCode = value.SalesmanCode;
        EditName = value.Name;
        EditPhone = value.Phone;
        EditIsActive = value.IsActive;
        IsEditing = true;
        EditPanelTitle = "Edit salesman";
        OnPropertyChanged(nameof(ShowEditIsActive));
    }

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            StatusMessage = "Loading salesmen…";
            var rows = await _salesmanService.ListAsync(SearchText);
            Salesmen.Clear();
            foreach (var row in rows)
                Salesmen.Add(row);
            StatusMessage = $"{rows.Count} salesman record(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load: " + ex.Message;
        }
    }

    [RelayCommand]
    private void NewSalesman()
    {
        SelectedSalesman = null;
        IsEditing = true;
        EditPanelTitle = "New salesman";
        _ = PrepareNewCodeAsync();
    }

    private async Task PrepareNewCodeAsync()
    {
        try
        {
            EditCode = await _codeGenerator.NextAsync();
            EditName = "";
            EditPhone = "";
            EditIsActive = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not generate code: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            MessageBox.Show("Salesman name is required.", "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusMessage = "Saving…";
            SalesmanSaveResult result;
            if (SelectedSalesman == null)
            {
                result = await _salesmanService.CreateAsync(EditName, EditPhone, EditCode);
            }
            else
            {
                result = await _salesmanService.UpdateAsync(SelectedSalesman, EditName, EditPhone, EditIsActive);
            }

            if (!string.IsNullOrWhiteSpace(result.CentralSyncWarning))
            {
                MessageBox.Show(result.CentralSyncWarning, "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Refresh();
            SelectedSalesman = Salesmen.FirstOrDefault(s =>
                s.SalesmanCode.Equals(result.Record.SalesmanCode, StringComparison.OrdinalIgnoreCase));
            StatusMessage = "Saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed: " + ex.Message;
            MessageBox.Show(ex.Message, "RR Bridal Billing", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (SelectedSalesman != null)
        {
            EditName = SelectedSalesman.Name;
            EditPhone = SelectedSalesman.Phone;
            EditIsActive = SelectedSalesman.IsActive;
            return;
        }

        ClearEditForm();
        IsEditing = false;
    }

    private void ClearEditForm()
    {
        EditCode = "";
        EditName = "";
        EditPhone = "";
        EditIsActive = true;
    }
}
