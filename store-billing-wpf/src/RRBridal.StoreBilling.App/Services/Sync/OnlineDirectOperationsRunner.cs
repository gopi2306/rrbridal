using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;
using RRBridal.StoreBilling.App.Services.Billing.Promotions;
using RRBridal.StoreBilling.App.Services.Invoicing;
using RRBridal.StoreBilling.App.Services.Masters;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed record OnlineDirectOperationsResult(
    int TransfersCompleted,
    int MastersRefreshed,
    int PromotionsRefreshed,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public string Message
    {
        get
        {
            var summary =
                $"Online refresh complete: {TransfersCompleted} transfer(s) completed, " +
                $"{MastersRefreshed} master list(s), {PromotionsRefreshed} promotion(s) refreshed.";
            return Errors.Count == 0
                ? summary
                : $"{summary} {Errors.Count} operation(s) failed: {string.Join(" | ", Errors.Take(3))}";
        }
    }
}

public interface IOnlineDirectOperationsRunner
{
    Task<OnlineDirectOperationsResult> RunAsync(
        bool includeStoreWideTransfers,
        CancellationToken ct = default);
}

public sealed class OnlineDirectOperationsRunner : IOnlineDirectOperationsRunner
{
    private readonly StoreContext _storeContext;
    private readonly CentralStorePosClient _storePos;
    private readonly MasterDataService _masterData;
    private readonly ReceiptConfigSyncService _receiptConfigSync;
    private readonly BarcodeLabelDesignSyncService _barcodeDesignSync;
    private readonly ShellBrandingService _shellBranding;
    private readonly PromotionSchemeRepository _promotions;

    public OnlineDirectOperationsRunner(
        StoreContext storeContext,
        CentralStorePosClient storePos,
        MasterDataService masterData,
        ReceiptConfigSyncService receiptConfigSync,
        BarcodeLabelDesignSyncService barcodeDesignSync,
        ShellBrandingService shellBranding,
        PromotionSchemeRepository promotions)
    {
        _storeContext = storeContext;
        _storePos = storePos;
        _masterData = masterData;
        _receiptConfigSync = receiptConfigSync;
        _barcodeDesignSync = barcodeDesignSync;
        _shellBranding = shellBranding;
        _promotions = promotions;
    }

    public async Task<OnlineDirectOperationsResult> RunAsync(
        bool includeStoreWideTransfers,
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        var transfersCompleted = 0;
        var mastersRefreshed = 0;
        var promotionsRefreshed = 0;

        if (includeStoreWideTransfers)
        {
            try
            {
                transfersCompleted = await CompleteAwaitingTransfersAsync(errors, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"transfers: {ex.Message}");
            }
        }

        try
        {
            var masters = await _masterData.RefreshOnlineCacheAsync(ct).ConfigureAwait(false);
            mastersRefreshed = masters.Refreshed;
            errors.AddRange(masters.Errors.Select(error => $"master {error}"));
        }
        catch (Exception ex)
        {
            errors.Add($"masters: {ex.Message}");
        }

        try
        {
            var receipt = await _receiptConfigSync
                .SyncReceiptFromCentralOnStoreSyncAsync(ct)
                .ConfigureAwait(false);
            if (!receipt.Ok)
                errors.Add($"receipt: {receipt.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"receipt: {ex.Message}");
        }

        try
        {
            var barcode = await _barcodeDesignSync
                .SyncFromCentralOnStoreSyncAsync(ct)
                .ConfigureAwait(false);
            if (!barcode.Ok)
                errors.Add($"barcode: {barcode.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"barcode: {ex.Message}");
        }

        try
        {
            promotionsRefreshed = await _promotions.RefreshOnlineCacheAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add($"promotions: {ex.Message}");
        }

        try
        {
            await _shellBranding.RefreshAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add($"branding: {ex.Message}");
        }

        return new OnlineDirectOperationsResult(
            transfersCompleted,
            mastersRefreshed,
            promotionsRefreshed,
            errors);
    }

    private async Task<int> CompleteAwaitingTransfersAsync(
        ICollection<string> errors,
        CancellationToken ct)
    {
        var transfers = await _storePos.ListAwaitingTransfersAsync(200, ct).ConfigureAwait(false);
        var completed = 0;
        foreach (var transfer in transfers)
        {
            try
            {
                var payload = new
                {
                    transferId = transfer.TransferId,
                    transferNo = transfer.TransferNo,
                    receivedAt = DateTime.UtcNow.ToString("O"),
                    lines = transfer.Lines.Select(line => new { line.Sku, line.Qty }).ToArray(),
                };
                var identity = string.IsNullOrWhiteSpace(transfer.TransferId)
                    ? transfer.TransferNo
                    : transfer.TransferId;
                var eventId = $"stock-transfer-received:{_storeContext.StoreId}:{identity}";
                await _storePos
                    .PostEventAsync("StockTransferReceived", payload, ct, eventId)
                    .ConfigureAwait(false);
                completed++;
            }
            catch (Exception ex)
            {
                errors.Add($"transfer {transfer.TransferNo}: {ex.Message}");
            }
        }

        return completed;
    }
}
