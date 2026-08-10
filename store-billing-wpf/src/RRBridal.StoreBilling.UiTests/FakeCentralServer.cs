using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RRBridal.StoreBilling.UiTests;

internal sealed class FakeCentralServer : IDisposable
{
    public const string ValidEmail = "ui.test@rrbridal.test";
    public const string ValidPassword = "UiTest123!";

    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _serveTask;

    private FakeCentralServer(HttpListener listener, Uri baseAddress, FakeCentralState state)
    {
        _listener = listener;
        BaseAddress = baseAddress;
        State = state;
        _listener.Start();
        _serveTask = Task.Run(ServeAsync);
    }

    public Uri BaseAddress { get; }

    public FakeCentralState State { get; }

    public string RequestSummary => State.RequestSummary;

    public static FakeCentralServer Start(FakeCentralScenario? scenario = null)
    {
        var port = ReservePort();
        var address = new Uri($"http://127.0.0.1:{port}/");
        var listener = new HttpListener();
        listener.Prefixes.Add(address.AbsoluteUri);
        return new FakeCentralServer(listener, address, new FakeCentralState(scenario ?? new()));
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _listener.Stop();
            _listener.Close();
            _serveTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Listener shutdown is best-effort.
        }
        finally
        {
            _stopping.Dispose();
        }
    }

    private async Task ServeAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stopping.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (_stopping.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var path = NormalizePath(request.Url?.AbsolutePath);
            var body = await ReadBodyAsync(request);
            State.Record(request.HttpMethod, request.RawUrl ?? path, body);

            if (request.HttpMethod == "POST" && path == "/api/auth/login")
            {
                await HandleLoginAsync(body, context.Response);
                return;
            }

            var (status, payload) = Route(request, path, body);
            await WriteJsonAsync(context.Response, status, payload);
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJsonAsync(
                    context.Response,
                    HttpStatusCode.InternalServerError,
                    new { message = "Fake central route failed.", detail = ex.Message });
            }
            catch
            {
                // The client may have disconnected.
            }
        }
    }

    private (HttpStatusCode Status, object? Payload) Route(
        HttpListenerRequest request,
        string path,
        string? body)
    {
        var method = request.HttpMethod;
        if (method == "GET")
            return RouteGet(request, path);

        var root = ParseBody(body);
        var domainPayload = UnwrapPayload(root);

        if (method == "POST" && path == "/api/store-pos/next-number")
            return Ok(new { value = State.NextNumber(ReadString(root, "kind") ?? "document") });
        if (method == "POST" && path == "/api/sync/push")
            return Ok(BuildSyncPushResponse(root));
        if (method == "POST" && path == "/api/store-pos/bills")
            State.StoreBill(domainPayload);
        else if (method == "DELETE" && TryTail(path, "/api/store-pos/bills/", out var billNo))
            State.DeleteBill(billNo);
        else if (method == "PUT" && TryTail(path, "/api/store-pos/held-bills/", out var holdNo))
            State.UpsertHeld(holdNo, domainPayload);
        else if (method == "DELETE" && TryTail(path, "/api/store-pos/held-bills/", out holdNo))
            State.DeleteHeld(holdNo);
        else if (method == "POST" && path == "/api/store-pos/quotations")
            State.StoreQuotation(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/sale-returns")
            State.StoreReturn(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/credit-notes")
            State.StoreCreditNote(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/day-sessions/open")
            State.OpenDaySession(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/day-sessions/close")
            State.CloseDaySession(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/cash-movements")
            State.StoreCashMovement(domainPayload);
        else if ((method == "POST" && path == "/api/store-pos/daily-expenses")
                 || (method == "PUT" && path.StartsWith("/api/store-pos/daily-expenses/", StringComparison.Ordinal)))
            State.StoreExpense(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/gateway-payments")
            State.StorePayment(domainPayload);
        else if (method == "POST" && path == "/api/store-pos/adjustment-bills")
            State.StoreAdjustment(domainPayload);

        return Ok(new { ok = true });
    }

    private (HttpStatusCode Status, object? Payload) RouteGet(HttpListenerRequest request, string path)
    {
        if (path == "/api/sync/store-pos-mode")
            return Ok(new
            {
                storeId = FakeCentralState.StoreId,
                preferCentralOnline = State.Scenario.PreferCentralOnline,
                posBillingSettings = new
                {
                    preferCentralOnline = State.Scenario.PreferCentralOnline,
                    screenAccess = new
                    {
                        knownCounters = new[] { "1" },
                        billing = new[] { "1" },
                        dashboard = new[] { "1" },
                    },
                },
                receiptPrintSettings = new { receiptCharWidth = 42 },
            });
        if (path == "/api/sync/health")
            return Ok(new { ok = true });
        if (path == "/api/sync/pull")
            return Ok(new
            {
                cursor = "ui-cursor-1",
                transferCursor = "0",
                promotionCursor = "0",
                adjustmentCursor = "0",
                updates = Array.Empty<object>(),
            });
        if (path == "/api/store-pos/catalog/search")
            return Ok(State.SearchCatalog(request.QueryString["q"] ?? ""));
        if (path == "/api/store-pos/bills")
            return Ok(State.BillsList);
        if (TryTail(path, "/api/store-pos/bills/", out var billNo)
            && !billNo.Contains('/'))
            return FoundOrNotFound(State.GetBill(billNo));
        if (path == "/api/store-pos/held-bills")
            return Ok(State.HeldBills);
        if (path == "/api/store-pos/quotations")
            return Ok(State.Quotations);
        if (TryTail(path, "/api/store-pos/quotations/", out var quotationNo))
            return FoundOrNotFound(State.GetQuotation(quotationNo));
        if (path == "/api/store-pos/sale-returns")
            return Ok(State.Returns);
        if (TryTail(path, "/api/store-pos/sale-returns/", out var returnNo))
            return FoundOrNotFound(State.GetReturn(returnNo));
        if (path == "/api/store-pos/credit-notes")
            return Ok(State.CreditNotes);
        if (TryTail(path, "/api/store-pos/credit-notes/", out var creditNo))
            return FoundOrNotFound(State.GetCreditNote(creditNo));
        if (path == "/api/store-pos/day-sessions/current")
            return State.DaySession is { } session
                ? Ok(new { payload = session })
                : (HttpStatusCode.NotFound, new { message = "No open day session." });
        if (path == "/api/store-pos/cash-movements")
            return Ok(State.CashMovements);
        if (path == "/api/store-pos/daily-expenses")
            return Ok(State.Expenses);
        if (path == "/api/store-pos/payment-receipts")
            return Ok(State.Payments);
        if (TryTail(path, "/api/store-pos/payment-receipts/", out var receiptNo))
            return FoundOrNotFound(State.GetPayment(receiptNo));
        if (path is "/api/store-pos/adjustment-bills" or "/api/store-pos/adjustment-bills/by-original-bill")
            return Ok(State.Adjustments);
        if (path is "/api/store-pos/gateway-payments" or "/api/store-pos/credit-note-cashouts"
            or "/api/store-pos/promotions" or "/api/store-pos/transfers/awaiting-intake")
            return Ok(Array.Empty<object>());
        if (path == "/api/customers")
            return Ok(new[] { CustomerPayload() });
        if (path == "/api/salesmen")
            return Ok(new[] { SalesmanPayload() });
        if (path == "/api/store-users")
            return Ok(new[] { StoreUserPayload() });
        if (path == "/api/whatsapp/settings")
            return Ok(new { enabled = false, configured = false });
        if (path.StartsWith("/api/whatsapp/broadcast/", StringComparison.Ordinal))
            return Ok(new { status = "completed", sent = 0, failed = 0 });
        if (path == "/api/company-profile")
            return Ok(CompanyPayload());
        if (path == $"/api/stores/{FakeCentralState.StoreId}")
            return Ok(StorePayload());
        if (path == $"/api/stores/{FakeCentralState.StoreId}/receipt-settings")
            return Ok(new { receiptCharWidth = 42, alwaysUsePrintDialog = true, printFormat = "Thermal" });
        if (path == "/api/barcode-label-designs/active")
            return Ok(new { });
        if (path == "/api/inventory/grid")
            return Ok(new
            {
                items = State.SearchCatalog(request.QueryString["search"] ?? ""),
                total = FakeCentralState.CatalogProducts.Count,
                page = 1,
                limit = 100,
            });
        if (path.StartsWith("/api/dashboard/", StringComparison.Ordinal))
            return Ok(new
            {
                summary = new { netSales = 0, expectedCash = 1000, billCount = 0 },
                bills = Array.Empty<object>(),
                items = Array.Empty<object>(),
                rows = Array.Empty<object>(),
            });

        // Master-data clients require arrays, while write endpoints require JSON objects.
        return Ok(Array.Empty<object>());
    }

    private async Task HandleLoginAsync(string? body, HttpListenerResponse response)
    {
        var root = ParseBody(body);
        var email = ReadString(root, "email");
        var password = ReadString(root, "password");
        if (!string.Equals(email, ValidEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(password, ValidPassword, StringComparison.Ordinal))
        {
            await WriteJsonAsync(
                response,
                HttpStatusCode.Unauthorized,
                new { message = "Invalid email or password." });
            return;
        }

        await WriteJsonAsync(
            response,
            HttpStatusCode.OK,
            new { accessToken = "ui-test-access-token", user = StoreUserPayload() });
    }

    private static object BuildSyncPushResponse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("events", out var events)
            || events.ValueKind != JsonValueKind.Array)
            return new { results = Array.Empty<object>() };

        return new
        {
            results = events.EnumerateArray().Select(item => new
            {
                eventId = ReadString(item, "eventId") ?? "",
                status = "applied",
            }).ToArray(),
        };
    }

    private static object StorePayload() => new
    {
        _id = FakeCentralState.StoreId,
        id = FakeCentralState.StoreId,
        code = FakeCentralState.StoreId,
        name = "UI Test Store",
        tradeName = "UI Test Store",
        preferCentralOnline = true,
    };

    private static object CompanyPayload() => new
    {
        tradeName = "RR Bridal UI Test",
        legalName = "RR Bridal UI Test",
        gstin = "29ABCDE1234F1Z5",
        state = "Karnataka",
        phone = "0000000000",
    };

    private static object CustomerPayload() => new
    {
        _id = FakeCentralState.CustomerId,
        customerCode = "CUST-UI-001",
        name = "UI Test Customer",
        phone = "9000000001",
        email = "customer@rrbridal.test",
        addressLine1 = "UI Test Address",
        city = "Bengaluru",
        state = "Karnataka",
        pincode = "560001",
        isCreditCustomer = true,
    };

    private static object SalesmanPayload() => new
    {
        _id = FakeCentralState.SalesmanId,
        storeId = FakeCentralState.StoreId,
        salesmanCode = "SM-UI-001",
        name = "UI Test Salesman",
        phone = "9000000002",
        isActive = true,
    };

    private static object StoreUserPayload() => new
    {
        _id = FakeCentralState.UserId,
        id = FakeCentralState.UserId,
        name = "UI Test User",
        email = ValidEmail,
        role = "admin",
        storeId = FakeCentralState.StoreId,
        maxDiscountPercent = 100,
        passwordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
    };

    private static (HttpStatusCode, object?) Ok(object? payload) => (HttpStatusCode.OK, payload);

    private static (HttpStatusCode, object?) FoundOrNotFound(JsonElement? payload) =>
        payload is { } value
            ? (HttpStatusCode.OK, value)
            : (HttpStatusCode.NotFound, new { message = "Not found." });

    private static string NormalizePath(string? path)
    {
        var normalized = (path ?? "").TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized;
    }

    private static bool TryTail(string path, string prefix, out string value)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = "";
            return false;
        }

        value = Uri.UnescapeDataString(path[prefix.Length..]);
        return value.Length > 0;
    }

    private static JsonElement ParseBody(string? body) =>
        string.IsNullOrWhiteSpace(body)
            ? JsonSerializer.SerializeToElement(new { })
            : JsonDocument.Parse(body).RootElement.Clone();

    private static JsonElement UnwrapPayload(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("payload", out var payload)
            ? payload.Clone()
            : root.Clone();

    private static string? ReadString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<string?> ReadBodyAsync(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
            return null;
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode status,
        object? payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        response.StatusCode = (int)status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static int ReservePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
