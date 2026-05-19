using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RRBridal.StoreBilling.App.Services;
using RRBridal.StoreBilling.App.Services.Notifications;
using RRBridal.StoreBilling.App.Services.Sync;

namespace RRBridal.StoreBilling.App.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty] private string _summaryText = "";

    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private string _footerNote = "";

    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<NotificationItem> Items { get; } = new();

    public NotificationsViewModel(AppServices services)
    {
        _services = services;
        FooterNote = services.StoreContext.IsPrimaryCounter
            ? "Open Settings (gear) for central login and receipt setup."
            : "";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var snap = await _services.OutboxNotifications.LoadAsync(CancellationToken.None);

            Items.Clear();
            foreach (var item in snap.Items)
                Items.Add(item);

            if (snap.ThisCounterPendingCount == 0)
            {
                SummaryText = "Nothing pending — all caught up with central.";
            }
            else
            {
                SummaryText = snap.ThisCounterPendingCount == 1
                    ? "1 item waiting to sync to central (this counter)."
                    : $"{snap.ThisCounterPendingCount} items waiting to sync to central (this counter).";
            }

            if (_services.StoreContext.IsPrimaryCounter && snap.StoreWidePendingCount > snap.ThisCounterPendingCount)
                SummaryText += $" Store-wide pending: {snap.StoreWidePendingCount}.";

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(snap.LastSyncError))
                parts.Add($"Last sync error: {snap.LastSyncError}");
            if (!string.IsNullOrWhiteSpace(snap.LastAutoSyncAt))
                parts.Add($"Last auto-sync: {snap.LastAutoSyncAt}");

            StatusText = parts.Count > 0 ? string.Join(Environment.NewLine, parts) : "";
        }
        catch (Exception ex)
        {
            SummaryText = "Could not load notifications.";
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task SyncNow()
    {
        IsBusy = true;
        StatusText = "Syncing…";
        try
        {
            var result = await _services.StoreSyncRunner.RunFullStoreSyncAsync(CancellationToken.None);
            StatusText = result.Message;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
