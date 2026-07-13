using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Api;
using RRBridal.StoreBilling.App.Services.Auth;
using RRBridal.StoreBilling.App.Services.BarcodePrinting;

namespace RRBridal.StoreBilling.App.Services.BarcodePrinting;

public sealed class BarcodeLabelDesignSyncService
{
    private readonly BarcodeLabelDesignClient _client;
    private readonly BarcodeLabelDesignStore _store;
    private readonly CentralAuthSession _authSession;

    public BarcodeLabelDesignSyncService(
        BarcodeLabelDesignClient client,
        BarcodeLabelDesignStore store,
        CentralAuthSession authSession)
    {
        _client = client;
        _store = store;
        _authSession = authSession;
    }

    public async Task<(bool Ok, string Message)> SyncFromCentralOnStoreSyncAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_authSession.AccessToken))
            return (false, "Central login required for barcode label design sync.");

        var (root, error) = await _client.GetActiveDesignAsync(ct).ConfigureAwait(false);
        if (error != null)
            return (false, error);
        if (root == null || root.Value.ValueKind != JsonValueKind.Object)
            return (false, "Barcode label design response was empty.");

        var document = JsonSerializer.Deserialize<BarcodeLabelDesignDocument>(
            root.Value.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (document?.Design == null)
            return (false, "Barcode label design payload missing design.");

        await _store.SaveAsync(document, ct).ConfigureAwait(false);
        return (true, $"Barcode label design synced: {document.Design.Name}");
    }
}
