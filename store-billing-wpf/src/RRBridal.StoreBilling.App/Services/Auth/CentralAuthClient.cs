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
        using var res = await _http.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, string.IsNullOrWhiteSpace(body) ? res.ReasonPhrase : body);
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("accessToken", out var tokenEl))
        {
            return (false, "Missing accessToken in response");
        }

        var token = tokenEl.GetString();
        _session.SetToken(token);
        _session.ApplyTo(_http);
        await _session.SaveToDiskAsync(ct);
        return (true, null);
    }

    public void Logout()
    {
        _session.ClearDiskAndToken();
        _session.ApplyTo(_http);
    }
}
