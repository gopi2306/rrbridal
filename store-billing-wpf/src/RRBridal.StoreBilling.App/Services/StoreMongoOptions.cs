using System;

namespace RRBridal.StoreBilling.App.Services;

/// <summary>
/// Env-tunable Mongo client settings for parent-Mongo / ZeroTier deployments.
/// </summary>
public sealed class StoreMongoOptions
{
    private const int DefaultConnectTimeoutSeconds = 20;
    private const int DefaultServerSelectionTimeoutSeconds = 20;
    private const int DefaultHeartbeatIntervalSeconds = 45;
    private const int MaxTimeoutSeconds = 120;

    public string ConnectionUri { get; }
    public TimeSpan ConnectTimeout { get; }
    public TimeSpan ServerSelectionTimeout { get; }
    public TimeSpan? SocketTimeout { get; }
    public TimeSpan HealthCheckInterval { get; }
    public string HostDisplay { get; }

    /// <summary>
    /// When true (ZeroTier / parent-Mongo mode), block app start until Mongo pings
    /// and block bill post while Mongo is offline.
    /// Set STORE_MONGO_REQUIRE_READY=false for local Mongo without that gate.
    /// </summary>
    public bool RequireReady { get; }

    public StoreMongoOptions()
        : this(
            Environment.GetEnvironmentVariable("STORE_MONGO_URI"),
            Environment.GetEnvironmentVariable("STORE_MONGO_CONNECT_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("STORE_MONGO_SERVER_SELECTION_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("STORE_MONGO_SOCKET_TIMEOUT_SECONDS"),
            Environment.GetEnvironmentVariable("STORE_MONGO_HEALTH_INTERVAL_SECONDS"),
            Environment.GetEnvironmentVariable("STORE_MONGO_REQUIRE_READY"))
    {
    }

    public StoreMongoOptions(
        string? connectionUri,
        string? connectTimeoutSeconds,
        string? serverSelectionTimeoutSeconds,
        string? socketTimeoutSeconds,
        string? healthIntervalSeconds,
        string? requireReady = null)
    {
        ConnectionUri = string.IsNullOrWhiteSpace(connectionUri)
            ? "mongodb://localhost:27017/rr_bridal_store"
            : connectionUri.Trim();

        ConnectTimeout = TimeSpan.FromSeconds(
            ParseSeconds(connectTimeoutSeconds, DefaultConnectTimeoutSeconds));
        ServerSelectionTimeout = TimeSpan.FromSeconds(
            ParseSeconds(serverSelectionTimeoutSeconds, DefaultServerSelectionTimeoutSeconds));

        var socketSeconds = ParseOptionalSeconds(socketTimeoutSeconds);
        SocketTimeout = socketSeconds.HasValue
            ? TimeSpan.FromSeconds(socketSeconds.Value)
            : null;

        HealthCheckInterval = TimeSpan.FromSeconds(
            ParseSeconds(healthIntervalSeconds, DefaultHeartbeatIntervalSeconds));

        // Default true: parent-Mongo / ZeroTier mode blocks start until reachable.
        // Set STORE_MONGO_REQUIRE_READY=false|0|no|off to allow start without the gate.
        RequireReady = ParseBool(requireReady, defaultValue: true);

        HostDisplay = BuildHostDisplay(ConnectionUri);
    }

    private static int ParseSeconds(string? raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var seconds))
            return fallback;

        if (seconds <= 0)
            return fallback;

        return Math.Min(seconds, MaxTimeoutSeconds);
    }

    private static int? ParseOptionalSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out var seconds))
            return null;

        if (seconds <= 0)
            return null;

        return Math.Min(seconds, MaxTimeoutSeconds);
    }

    internal static bool ParseBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                return false;
            default:
                return defaultValue;
        }
    }

    private static string BuildHostDisplay(string uri)
    {
        try
        {
            var url = new MongoDB.Driver.MongoUrl(uri);
            if (!string.IsNullOrWhiteSpace(url.Server?.Host))
            {
                var port = url.Server.Port > 0 ? url.Server.Port : 27017;
                return $"{url.Server.Host}:{port}";
            }
        }
        catch
        {
            /* fall through */
        }

        return uri;
    }
}
