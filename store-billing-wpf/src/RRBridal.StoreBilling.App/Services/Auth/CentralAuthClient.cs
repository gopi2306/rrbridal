using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Auth;

public sealed class CentralAuthClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthSession _session;

    public CentralAuthClient(HttpClient http, CentralAuthSession session)
    {
        _http = http;
        _session = session;
    }

    public async Task<(bool ok, string? errorMessage)> LoginAsync(string email, string password, CancellationToken ct)
    {
        var (ok, error, _) = await LoginWithUserAsync(email, password, ct).ConfigureAwait(false);
        return (ok, error);
    }

    public async Task<(bool ok, string? errorMessage, StoreUserRecord? user)> LoginWithUserAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : TruncateError(body), null);
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("accessToken", out var tokenEl))
        {
            return (false, "Missing accessToken in response", null);
        }

        var token = tokenEl.GetString();
        _session.SetToken(token);
        _session.ApplyTo(_http);
        await _session.SaveToDiskAsync(ct);

        StoreUserRecord? user = null;
        if (doc.RootElement.TryGetProperty("user", out var userEl) && userEl.ValueKind == JsonValueKind.Object)
            user = MapUser(userEl, email);

        user ??= new StoreUserRecord
        {
            Email = email.Trim().ToLowerInvariant(),
            Name = email.Trim(),
            Role = "store",
        };

        return (true, null, user);
    }

    public void Logout()
    {
        _session.ClearDiskAndToken();
        _session.ApplyTo(_http);
    }

    private static StoreUserRecord MapUser(JsonElement userEl, string fallbackEmail)
    {
        static string ReadString(JsonElement el, string name, string fallback = "")
        {
            if (!el.TryGetProperty(name, out var p))
                return fallback;
            if (p.ValueKind == JsonValueKind.String)
                return p.GetString()?.Trim() ?? fallback;
            if (p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                return p.ToString();
            return fallback;
        }

        static decimal ReadMaxDiscount(JsonElement el)
        {
            if (!el.TryGetProperty("maxDiscountPercent", out var p) || p.ValueKind == JsonValueKind.Null)
                return 100m;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
            {
                if (d < 0) return 0;
                if (d > 100) return 100;
                return d;
            }
            return 100m;
        }

        var id = ReadString(userEl, "_id");
        if (string.IsNullOrWhiteSpace(id))
            id = ReadString(userEl, "id");

        var email = ReadString(userEl, "email", fallbackEmail.Trim().ToLowerInvariant());
        var name = ReadString(userEl, "name");
        if (string.IsNullOrWhiteSpace(name))
            name = email;

        return new StoreUserRecord
        {
            CentralId = id,
            Name = name,
            Email = email,
            Role = ReadString(userEl, "role", "store"),
            StoreId = ReadString(userEl, "storeId"),
            MaxDiscountPercent = ReadMaxDiscount(userEl),
        };
    }

    private static string TruncateError(string body)
    {
        var t = body.Trim();
        try
        {
            using var doc = JsonDocument.Parse(t);
            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                if (msg.ValueKind == JsonValueKind.String)
                    return msg.GetString() ?? t;
                if (msg.ValueKind == JsonValueKind.Array && msg.GetArrayLength() > 0)
                    return msg[0].GetString() ?? t;
            }
        }
        catch
        {
            /* raw body */
        }

        if (t.Length > 240)
            return t[..240] + "…";
        return t;
    }
}
