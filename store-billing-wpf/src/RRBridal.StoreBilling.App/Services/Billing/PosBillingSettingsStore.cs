using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Billing;

public sealed class PosBillingSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly string _tempFilePath;
    private PosBillingSettingsDocument _current = new();

    public PosBillingSettingsStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RRBridal", "StoreBilling");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "billing_settings.json");
        _tempFilePath = _filePath + ".tmp";
        Load();
    }

    public PosBillingSettingsDocument Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Raised after a successful SaveAsync so Shell can refresh nav visibility.</summary>
    public event Action? Changed;

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    _current = new PosBillingSettingsDocument();
                }
                else
                {
                    var json = File.ReadAllText(_filePath);
                    _current = string.IsNullOrWhiteSpace(json)
                        ? new PosBillingSettingsDocument()
                        : JsonSerializer.Deserialize<PosBillingSettingsDocument>(json, JsonOpts) ?? new PosBillingSettingsDocument();
                }

                _current.ScreenAccess ??= CounterScreenAccessSettings.CreateDefaults();
                ApplyEnvOnlineModeOverrideUnlocked();
            }
            catch
            {
                _current = new PosBillingSettingsDocument();
                _current.ScreenAccess ??= CounterScreenAccessSettings.CreateDefaults();
                ApplyEnvOnlineModeOverrideUnlocked();
            }
        }
    }

    /// <summary>
    /// When set, <c>PREFER_CENTRAL_ONLINE</c> (or <c>STORE_POS_MODE=online|offline</c>) wins over
    /// billing_settings.json. Online = direct CENTRAL_API_BASE (no ZeroTier / parent Mongo).
    /// </summary>
    public bool? EnvOnlineOverride { get; private set; }

    public bool IsEnvOnlineOverride => EnvOnlineOverride.HasValue;

    private void ApplyEnvOnlineModeOverrideUnlocked()
    {
        EnvOnlineOverride = ReadEnvOnlineMode();
        if (EnvOnlineOverride is null)
            return;

        _current.PreferCentralOnline = EnvOnlineOverride.Value;
    }

    /// <summary>
    /// Re-apply .env Online/Offline after Settings saves so env always wins at runtime.
    /// </summary>
    public void ReapplyEnvOnlineModeOverride()
    {
        lock (_lock)
        {
            ApplyEnvOnlineModeOverrideUnlocked();
        }
    }

    internal static bool? ReadEnvOnlineMode()
    {
        var prefer = Environment.GetEnvironmentVariable("PREFER_CENTRAL_ONLINE");
        if (!string.IsNullOrWhiteSpace(prefer))
            return StoreMongoOptions.ParseBool(prefer, defaultValue: false);

        var mode = Environment.GetEnvironmentVariable("STORE_POS_MODE")?.Trim();
        if (string.IsNullOrWhiteSpace(mode))
            return null;

        switch (mode.ToLowerInvariant())
        {
            case "online":
            case "central":
            case "central-online":
                return true;
            case "offline":
            case "local":
                return false;
            default:
                return null;
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        string json;
        lock (_lock)
        {
            _current.ScreenAccess ??= CounterScreenAccessSettings.CreateDefaults();
            json = JsonSerializer.Serialize(_current, JsonOpts);
        }

        await File.WriteAllTextAsync(_tempFilePath, json, ct).ConfigureAwait(false);
        lock (_lock)
        {
            if (File.Exists(_filePath))
                File.Replace(_tempFilePath, _filePath, null);
            else
                File.Move(_tempFilePath, _filePath);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Replace local billing settings from central (store-wide). .env Online/Offline still wins.
    /// </summary>
    public void ApplyFromCentral(PosBillingSettingsDocument central)
    {
        lock (_lock)
        {
            var keepOnline = IsEnvOnlineOverride ? _current.PreferCentralOnline : (bool?)null;
            _current.PreferCentralOnline = central.PreferCentralOnline;
            _current.AllowDuplicatePrint = central.AllowDuplicatePrint;
            _current.ConfirmDuplicateProductAdd = central.ConfirmDuplicateProductAdd;
            _current.AllowCreditNoteRemainingCashout = central.AllowCreditNoteRemainingCashout;
            _current.LineItemDetailLevel = central.LineItemDetailLevel;
            _current.AlterationGstIncluded = central.AlterationGstIncluded;
            _current.AllowMultipleReturnsPerBill = central.AllowMultipleReturnsPerBill;
            _current.EnableCreditBilling = central.EnableCreditBilling;
            _current.CreditBillingRequireCreditCustomer = central.CreditBillingRequireCreditCustomer;
            _current.CreditBillingMinimumAdvancePercent = central.CreditBillingMinimumAdvancePercent;
            _current.CreditBillingMinimumAdvanceAmount = central.CreditBillingMinimumAdvanceAmount;
            _current.CreditBillingAllowZeroAdvance = central.CreditBillingAllowZeroAdvance;
            _current.CreditBillingAllowPartialCollection = central.CreditBillingAllowPartialCollection;
            _current.CreditBillingMaxBalancePerBill = central.CreditBillingMaxBalancePerBill;
            _current.ScreenAccess = central.ScreenAccess ?? CounterScreenAccessSettings.CreateDefaults();
            ApplyEnvOnlineModeOverrideUnlocked();
            if (keepOnline.HasValue)
                _current.PreferCentralOnline = keepOnline.Value;
        }
    }

    public void Update(Action<PosBillingSettingsDocument> mutate)
    {
        lock (_lock)
        {
            mutate(_current);
        }
    }
}
