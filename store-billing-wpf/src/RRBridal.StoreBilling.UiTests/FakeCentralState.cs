using System.Collections.Concurrent;
using System.Text.Json;
using MongoDB.Bson;

namespace RRBridal.StoreBilling.UiTests;

internal sealed record FakeCentralScenario
{
    public bool PreferCentralOnline { get; init; } = true;

    public bool SeedOpenDaySession { get; init; } = true;
}

internal sealed record FakeCentralRequest(
    DateTimeOffset ReceivedAt,
    string Method,
    string PathAndQuery,
    string? Body);

internal sealed record FakeCatalogProduct(
    string CentralId,
    string Sku,
    string UpcEanCode,
    string Name,
    string ShortName,
    string Alias,
    decimal CostPrice,
    decimal Mrp,
    decimal SellingPrice,
    decimal StorePrice,
    decimal GstPercent,
    string HsnSac,
    decimal StockQty);

internal sealed class FakeCentralState
{
    public const string StoreId = "ui-test-store";
    public const string UserId = "ui-test-user";
    public const string CustomerId = "ui-test-customer";
    public const string SalesmanId = "ui-test-salesman";

    public static IReadOnlyList<FakeCatalogProduct> CatalogProducts { get; } =
    [
        new(
            "ui-product-1", "UI-SAREE-001", "890000000001",
            "UI Test Bridal Saree", "Bridal Saree", "UIBRIDAL",
            2500m, 5000m, 4500m, 4400m, 5m, "5407", 25m),
        new(
            "ui-product-2", "UI-BLOUSE-001", "890000000002",
            "UI Test Designer Blouse", "Designer Blouse", "UIBLOUSE",
            600m, 1500m, 1250m, 1200m, 5m, "6206", 40m),
    ];

    private readonly ConcurrentQueue<FakeCentralRequest> _requests = new();
    private readonly ConcurrentDictionary<string, JsonElement> _bills = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _heldBills = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _quotations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _returns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _creditNotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _expenses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _cashMovements = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _payments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JsonElement> _adjustments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.OrdinalIgnoreCase);
    private JsonElement? _daySession;

    public FakeCentralState(FakeCentralScenario scenario)
    {
        Scenario = scenario;
        if (scenario.SeedOpenDaySession)
            _daySession = JsonSerializer.SerializeToElement(CreateOpenDaySessionPayload());
    }

    public FakeCentralScenario Scenario { get; }

    public IReadOnlyList<FakeCentralRequest> Requests => _requests.ToArray();

    public IReadOnlyDictionary<string, JsonElement> Bills => _bills;

    public string RequestSummary => string.Join(
        ", ",
        _requests.Select(request => $"{request.Method} {request.PathAndQuery}"));

    public void Record(string method, string pathAndQuery, string? body) =>
        _requests.Enqueue(new FakeCentralRequest(DateTimeOffset.UtcNow, method, pathAndQuery, body));

    public string NextNumber(string kind)
    {
        var normalized = string.IsNullOrWhiteSpace(kind) ? "document" : kind.Trim().ToLowerInvariant();
        var next = _counters.AddOrUpdate(normalized, 1, static (_, current) => current + 1);
        var prefix = normalized switch
        {
            "bill" or "billno" => "BILL",
            "hold" or "held" or "holdno" => "HOLD",
            "quotation" or "quotationno" => "QUOT",
            "return" or "sale_return" or "returnno" => "RET",
            "credit_note" or "creditnoteno" => "CN",
            "cash_movement" or "cashmovementno" => "CMV",
            "daily_expense" or "expense" or "expenseno" => "EXP",
            "payment_receipt" or "paymentreceiptno" => "RCPT",
            "adjustment" or "adjustmentno" => "ADJ",
            _ => normalized.Replace('_', '-').ToUpperInvariant(),
        };
        return $"{prefix}-UI-{next:0000}";
    }

    public IReadOnlyList<object> SearchCatalog(string query)
    {
        var q = query?.Trim() ?? "";
        return CatalogProducts
            .Where(product =>
                product.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)
                || product.UpcEanCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                || product.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || product.Alias.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(product => (object)new
            {
                centralId = product.CentralId,
                sku = product.Sku,
                upcEanCode = product.UpcEanCode,
                name = product.Name,
                shortName = product.ShortName,
                alias = product.Alias,
                costPrice = product.CostPrice,
                mrp = product.Mrp,
                sellingPrice = product.SellingPrice,
                storePrice = product.StorePrice,
                gstPercent = product.GstPercent,
                hsnSac = product.HsnSac,
                stockQty = product.StockQty,
                mediaItems = Array.Empty<object>(),
            })
            .ToArray();
    }

    public JsonElement? DaySession => _daySession;

    public void OpenDaySession(JsonElement payload) => _daySession = Clone(payload);

    public void CloseDaySession(JsonElement payload) => _daySession = Clone(payload);

    public void UpsertHeld(string key, JsonElement payload) => _heldBills[key] = Clone(payload);

    public bool DeleteHeld(string key) => _heldBills.TryRemove(key, out _);

    public IReadOnlyList<JsonElement> HeldBills => _heldBills.Values.ToArray();

    public IReadOnlyList<JsonElement> BillsList => _bills.Values.ToArray();

    public IReadOnlyList<JsonElement> Quotations => _quotations.Values.ToArray();

    public IReadOnlyList<JsonElement> Returns => _returns.Values.ToArray();

    public IReadOnlyList<JsonElement> CreditNotes => _creditNotes.Values.ToArray();

    public IReadOnlyList<JsonElement> Expenses => _expenses.Values.ToArray();

    public IReadOnlyList<JsonElement> CashMovements => _cashMovements.Values.ToArray();

    public IReadOnlyList<JsonElement> Payments => _payments.Values.ToArray();

    public IReadOnlyList<JsonElement> Adjustments => _adjustments.Values.ToArray();

    public JsonElement? GetBill(string key) => TryGet(_bills, key);
    public JsonElement? GetQuotation(string key) => TryGet(_quotations, key);
    public JsonElement? GetReturn(string key) => TryGet(_returns, key);
    public JsonElement? GetCreditNote(string key) => TryGet(_creditNotes, key);
    public JsonElement? GetPayment(string key) => TryGet(_payments, key);

    public void StoreBill(JsonElement payload) => StoreByKey(_bills, payload, "billNo", NextNumber("bill"));
    public void StoreQuotation(JsonElement payload) => StoreByKey(_quotations, payload, "quotationNo", NextNumber("quotation"));
    public void StoreReturn(JsonElement payload) => StoreByKey(_returns, payload, "returnNo", NextNumber("return"));
    public void StoreCreditNote(JsonElement payload) => StoreByKey(_creditNotes, payload, "creditNoteNo", NextNumber("credit_note"));
    public void StoreExpense(JsonElement payload) => StoreByKey(_expenses, payload, "expenseNo", NextNumber("expense"));
    public void StoreCashMovement(JsonElement payload) => StoreByKey(_cashMovements, payload, "movementNo", NextNumber("cash_movement"));
    public void StorePayment(JsonElement payload) => StoreByKey(_payments, payload, "receiptNo", NextNumber("payment_receipt"));
    public void StoreAdjustment(JsonElement payload) => StoreByKey(_adjustments, payload, "adjustmentNo", NextNumber("adjustment"));

    public bool DeleteBill(string key) => _bills.TryRemove(key, out _);

    public static BsonDocument CreateOpenDaySessionDocument()
    {
        var payload = CreateOpenDaySessionPayload();
        return BsonDocument.Parse(JsonSerializer.Serialize(payload));
    }

    private static object CreateOpenDaySessionPayload() => new
    {
        sessionId = "ui-test-session",
        storeId = StoreId,
        posCounter = "1",
        deviceId = "ui-test-device",
        businessDate = DateTime.Today.ToString("yyyy-MM-dd"),
        status = "open",
        openingCash = 1000m,
        expectedCash = 1000m,
        actualCashCounted = 0m,
        cashDifference = 0m,
        openedBy = "UI Test User",
        openedAtUtc = DateTime.UtcNow.ToString("O"),
    };

    private static JsonElement? TryGet(
        ConcurrentDictionary<string, JsonElement> source,
        string key) =>
        source.TryGetValue(key, out var value) ? value : null;

    private static void StoreByKey(
        ConcurrentDictionary<string, JsonElement> target,
        JsonElement payload,
        string property,
        string fallback)
    {
        var key = payload.ValueKind == JsonValueKind.Object
                  && payload.TryGetProperty(property, out var element)
                  && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        target[string.IsNullOrWhiteSpace(key) ? fallback : key] = Clone(payload);
    }

    private static JsonElement Clone(JsonElement element) =>
        JsonDocument.Parse(element.GetRawText()).RootElement.Clone();
}
