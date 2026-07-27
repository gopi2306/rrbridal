using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RRBridal.StoreBilling.App.Services.Products;

namespace RRBridal.StoreBilling.App.Services.Api;

public sealed class CentralStorePosClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly StoreContext _storeContext;

    public CentralStorePosClient(HttpClient http, StoreContext storeContext)
    {
        _http = http;
        _storeContext = storeContext;
    }

    public async Task<IReadOnlyList<CatalogProduct>> SearchCatalogAsync(string query, CancellationToken ct = default)
    {
        var q = query?.Trim() ?? "";
        if (q.Length < 1)
            return Array.Empty<CatalogProduct>();

        var url =
            $"/api/store-pos/catalog/search?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&q={Uri.EscapeDataString(q)}&limit=80";
        using var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Central catalog search failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var rows = await JsonSerializer.DeserializeAsync<List<StorePosCatalogDto>>(stream, JsonOpts, ct)
                   ?? [];
        var list = new List<CatalogProduct>();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Sku))
                continue;
            list.Add(new CatalogProduct
            {
                CentralId = row.CentralId ?? "",
                Sku = row.Sku,
                UpcEanCode = row.UpcEanCode,
                Name = string.IsNullOrWhiteSpace(row.Name) ? row.Sku : row.Name!,
                ShortName = row.ShortName,
                Alias = row.Alias,
                CostPrice = row.CostPrice,
                MarginPercent = row.MarginPercent,
                Mrp = row.Mrp,
                SellingPrice = row.SellingPrice,
                StorePrice = row.StorePrice ?? row.SellingPrice,
                GstPercent = row.GstPercent,
                HsnSac = row.HsnSac,
                CategoryId = row.CategoryId,
                BrandId = row.BrandId,
                OfferGroupId = row.OfferGroupId,
                StockQty = row.StockQty,
                MediaItems = MapMedia(row.MediaItems),
            });
        }

        return list;
    }

    public Task PostBillAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/bills", payload, ct);

    public Task DeleteBillAsync(string billNo, object? payload = null, CancellationToken ct = default) =>
        SendDeleteAsync($"/api/store-pos/bills/{Uri.EscapeDataString(billNo)}", payload, ct);

    public Task PostSaleReturnAsync(object payload, bool exchange = false, CancellationToken ct = default) =>
        PostAsync("/api/store-pos/sale-returns", new
        {
            storeId = _storeContext.StoreId,
            deviceId = _storeContext.DeviceId,
            exchange,
            payload,
        }, ct);

    public Task PostQuotationAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/quotations", payload, ct);

    public Task ConvertQuotationAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/quotations/convert", payload, ct);

    public Task CancelQuotationAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/quotations/cancel", payload, ct);

    public Task PostCreditNoteAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/credit-notes", payload, ct);

    public Task ApplyCreditNoteAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/credit-notes/apply", payload, ct);

    public Task CashoutCreditNoteAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/credit-notes/cashout", payload, ct);

    public Task OpenDaySessionAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/day-sessions/open", payload, ct);

    public Task CloseDaySessionAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/day-sessions/close", payload, ct);

    public Task PostCashMovementAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/cash-movements", payload, ct);

    public Task PostDailyExpenseAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/daily-expenses", payload, ct);

    public Task PostCodPaymentAsync(string billNo, object payload, CancellationToken ct = default) =>
        PostWriteAsync($"/api/store-pos/bills/{Uri.EscapeDataString(billNo)}/cod-payment", payload, ct);

    public Task PostCreditPaymentAsync(string billNo, object payload, CancellationToken ct = default) =>
        PostWriteAsync($"/api/store-pos/bills/{Uri.EscapeDataString(billNo)}/credit-payment", payload, ct);

    public Task PostAdjustmentBillAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/adjustment-bills", payload, ct);

    public Task PostEventAsync(string type, object payload, CancellationToken ct = default) =>
        PostAsync("/api/store-pos/events", new
        {
            type,
            storeId = _storeContext.StoreId,
            deviceId = _storeContext.DeviceId,
            payload,
        }, ct);

    public async Task<string> NextNumberAsync(string kind, CancellationToken ct = default)
    {
        using var res = await _http.PostAsJsonAsync("/api/store-pos/next-number", new
        {
            storeId = _storeContext.StoreId,
            deviceId = _storeContext.DeviceId,
            posCounter = _storeContext.PosCounter,
            kind,
        }, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central next-number failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("value", out var valueEl))
            throw new InvalidOperationException("Central next-number response missing value.");
        return valueEl.GetString()
               ?? throw new InvalidOperationException("Central next-number returned empty value.");
    }

    public async Task<JsonDocument> GetBillAsync(string billNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/bills/{Uri.EscapeDataString(billNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central get bill failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListBillsAsync(string? search = null, int limit = 50, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/bills?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central list bills failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> GetDaySessionAsync(string businessDate, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/day-sessions/current?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&businessDate={Uri.EscapeDataString(businessDate)}" +
            $"&posCounter={Uri.EscapeDataString(_storeContext.PosCounter)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central day session failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task UpsertHeldBillAsync(string holdNo, object payload, CancellationToken ct = default)
    {
        using var res = await _http.PutAsJsonAsync(
            $"/api/store-pos/held-bills/{Uri.EscapeDataString(holdNo.Trim())}",
            new
            {
                storeId = _storeContext.StoreId,
                deviceId = _storeContext.DeviceId,
                payload,
            },
            ct);
        if (res.IsSuccessStatusCode)
            return;
        var raw = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Central held-bill save failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
    }

    public async Task DeleteHeldBillAsync(string holdNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/held-bills/{Uri.EscapeDataString(holdNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.DeleteAsync(url, ct);
        if (res.IsSuccessStatusCode)
            return;
        var raw = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Central held-bill delete failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
    }

    public async Task<JsonDocument> ListHeldBillsAsync(CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/held-bills?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&deviceId={Uri.EscapeDataString(_storeContext.DeviceId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central held-bills list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListQuotationsAsync(string? status = null, int limit = 50, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/quotations?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(status))
            url += $"&status={Uri.EscapeDataString(status.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central quotations list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListCreditNotesAsync(
        string? customerPhone = null,
        string? customerCode = null,
        bool availableOnly = false,
        CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/credit-notes?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&availableOnly={(availableOnly ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(customerPhone))
            url += $"&customerPhone={Uri.EscapeDataString(customerPhone.Trim())}";
        if (!string.IsNullOrWhiteSpace(customerCode))
            url += $"&customerCode={Uri.EscapeDataString(customerCode.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central credit-notes list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> GetCreditNoteAsync(string creditNoteNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/credit-notes/{Uri.EscapeDataString(creditNoteNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central get credit-note failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> GetQuotationAsync(string quotationNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/quotations/{Uri.EscapeDataString(quotationNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central get quotation failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListSaleReturnsAsync(
        string? originalBillNo = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/sale-returns?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(originalBillNo))
            url += $"&originalBillNo={Uri.EscapeDataString(originalBillNo.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central sale-returns list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> GetSaleReturnAsync(string returnNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/sale-returns/{Uri.EscapeDataString(returnNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central get sale-return failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListCashMovementsAsync(string businessDate, int limit = 100, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/cash-movements?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&businessDate={Uri.EscapeDataString(businessDate.Trim())}&limit={limit}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central cash-movements list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListDailyExpensesAsync(string businessDate, int limit = 100, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/daily-expenses?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&businessDate={Uri.EscapeDataString(businessDate.Trim())}&limit={limit}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central daily-expenses list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public Task ApproveStockExceptionsAsync(string billNo, object payload, CancellationToken ct = default) =>
        PostWriteAsync(
            $"/api/store-pos/bills/{Uri.EscapeDataString(billNo.Trim())}/stock-exceptions/approve",
            payload,
            ct);

    public Task UpdateBillWhatsAppAsync(string billNo, object payload, CancellationToken ct = default) =>
        PostWriteAsync($"/api/store-pos/bills/{Uri.EscapeDataString(billNo.Trim())}/whatsapp", payload, ct);

    public Task AppendBillPrintAuditAsync(string billNo, object payload, CancellationToken ct = default) =>
        PostWriteAsync($"/api/store-pos/bills/{Uri.EscapeDataString(billNo.Trim())}/print-audit", payload, ct);

    public Task MarkCashHandOverPrintedAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/day-sessions/cash-handover-printed", payload, ct);

    public Task PostGatewayPaymentAsync(object payload, CancellationToken ct = default) =>
        PostWriteAsync("/api/store-pos/gateway-payments", payload, ct);

    public async Task<JsonDocument> GetPaymentReceiptAsync(string receiptNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/payment-receipts/{Uri.EscapeDataString(receiptNo.Trim())}?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central payment receipt failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListAdjustmentsAsync(string? originalBillNo = null, int limit = 100, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/adjustment-bills?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(originalBillNo))
            url += $"&originalBillNo={Uri.EscapeDataString(originalBillNo.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central adjustments list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> GetAdjustmentByOriginalBillAsync(string originalBillNo, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/adjustment-bills/by-original-bill?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}" +
            $"&originalBillNo={Uri.EscapeDataString(originalBillNo.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound || string.IsNullOrWhiteSpace(raw) || raw == "null")
            return JsonDocument.Parse("null");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central adjustment lookup failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListGatewayPaymentsAsync(int limit = 200, string? posCounter = null, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/gateway-payments?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(posCounter))
            url += $"&posCounter={Uri.EscapeDataString(posCounter.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central gateway-payments list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListCreditNoteCashoutsAsync(string? businessDate = null, int limit = 100, CancellationToken ct = default)
    {
        var url =
            $"/api/store-pos/credit-note-cashouts?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(businessDate))
            url += $"&businessDate={Uri.EscapeDataString(businessDate.Trim())}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central credit-note-cashouts list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    public async Task<JsonDocument> ListActivePromotionsAsync(CancellationToken ct = default)
    {
        var url = $"/api/store-pos/promotions?storeCode={Uri.EscapeDataString(_storeContext.StoreId)}";
        using var res = await _http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Central promotions list failed ({(int)res.StatusCode}): {Truncate(raw, 300)}");
        return JsonDocument.Parse(raw);
    }

    private Task PostWriteAsync(string url, object payload, CancellationToken ct) =>
        PostAsync(url, new
        {
            storeId = _storeContext.StoreId,
            deviceId = _storeContext.DeviceId,
            payload,
        }, ct);

    private async Task PostAsync(string url, object body, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync(url, body, ct);
        if (res.IsSuccessStatusCode)
            return;

        var raw = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Central store-pos failed ({(int)res.StatusCode}): {Truncate(raw, 400)}");
    }

    private async Task SendDeleteAsync(string url, object? payload, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(new
            {
                storeId = _storeContext.StoreId,
                deviceId = _storeContext.DeviceId,
                payload = payload ?? new { },
            }),
        };
        using var res = await _http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
            return;

        var raw = await res.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Central store-pos delete failed ({(int)res.StatusCode}): {Truncate(raw, 400)}");
    }

    private static IReadOnlyList<ProductMediaItem> MapMedia(List<StorePosMediaDto>? items)
    {
        if (items == null || items.Count == 0)
            return Array.Empty<ProductMediaItem>();
        var list = new List<ProductMediaItem>();
        foreach (var m in items)
        {
            if (string.IsNullOrWhiteSpace(m.Url))
                continue;
            list.Add(new ProductMediaItem { Url = m.Url, Description = m.Description });
        }
        return list;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    private sealed class StorePosCatalogDto
    {
        public string? CentralId { get; set; }
        public string? Sku { get; set; }
        public string? UpcEanCode { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Alias { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? MarginPercent { get; set; }
        public decimal? Mrp { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal? StorePrice { get; set; }
        public decimal? GstPercent { get; set; }
        public string? HsnSac { get; set; }
        public decimal StockQty { get; set; }
        public string? CategoryId { get; set; }
        public string? BrandId { get; set; }
        public string? OfferGroupId { get; set; }
        public List<StorePosMediaDto>? MediaItems { get; set; }
    }

    private sealed class StorePosMediaDto
    {
        public string? Url { get; set; }
        public string? Description { get; set; }
    }
}
