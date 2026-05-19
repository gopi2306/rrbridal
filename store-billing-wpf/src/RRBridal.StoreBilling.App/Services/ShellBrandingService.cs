using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.Invoicing;

namespace RRBridal.StoreBilling.App.Services;

public sealed class ShellBrandingService
{
    private readonly ReceiptConfigStore _receiptConfig;
    private readonly StoreContext _storeContext;
    private readonly StoreInfoClient _storeInfo;
    private readonly CentralAuthSession _authSession;
    private readonly IMongoCollection<BsonDocument> _syncState;

    public ShellBrandingSnapshot Current { get; private set; } = new();

    public event Action? BrandingChanged;

    public ShellBrandingService(
        ReceiptConfigStore receiptConfig,
        StoreContext storeContext,
        StoreInfoClient storeInfo,
        CentralAuthSession authSession,
        IMongoDatabase localDb)
    {
        _receiptConfig = receiptConfig;
        _storeContext = storeContext;
        _storeInfo = storeInfo;
        _authSession = authSession;
        _syncState = localDb.GetCollection<BsonDocument>("sync_state");
    }

    public async Task<ShellBrandingSnapshot> RefreshAsync(CancellationToken ct = default)
    {
        _receiptConfig.Reload();

        var company = _receiptConfig.Current.Store.StoreName?.Trim();
        if (string.IsNullOrWhiteSpace(company))
            company = "RR Bridal";

        var tillLine = $"POS{_storeContext.PosCounter} · {_storeContext.DeviceId}";

        var storeName = await LoadCachedStoreDisplayNameAsync(ct);
        if (string.IsNullOrWhiteSpace(storeName) && !string.IsNullOrEmpty(_authSession.AccessToken))
        {
            var (fetched, _) = await _storeInfo.GetStoreNameAsync(_storeContext.StoreId, ct);
            if (!string.IsNullOrWhiteSpace(fetched))
            {
                storeName = fetched;
                await SaveCachedStoreDisplayNameAsync(storeName, ct);
            }
        }

        if (string.IsNullOrWhiteSpace(storeName))
            storeName = _storeContext.StoreId;

        var windowTitle = $"{company} — {storeName}";

        Current = new ShellBrandingSnapshot
        {
            CompanyTitle = company,
            StoreDisplayName = storeName,
            TillDisplayLine = tillLine,
            WindowTitleText = windowTitle,
        };

        BrandingChanged?.Invoke();
        return Current;
    }

    private async Task<string?> LoadCachedStoreDisplayNameAsync(CancellationToken ct)
    {
        try
        {
            var doc = await _syncState.Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync(ct);
            if (doc?.TryGetValue("storeDisplayName", out var v) == true && v.IsString)
            {
                var s = v.AsString.Trim();
                return string.IsNullOrEmpty(s) ? null : s;
            }
        }
        catch { /* best-effort */ }

        return null;
    }

    private async Task SaveCachedStoreDisplayNameAsync(string storeDisplayName, CancellationToken ct)
    {
        try
        {
            await _syncState.UpdateOneAsync(
                FilterDefinition<BsonDocument>.Empty,
                Builders<BsonDocument>.Update.Set("storeDisplayName", storeDisplayName),
                new UpdateOptions { IsUpsert = true },
                ct);
        }
        catch { /* best-effort */ }
    }
}
