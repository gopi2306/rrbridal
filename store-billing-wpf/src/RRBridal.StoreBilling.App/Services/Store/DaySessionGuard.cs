using System;
using System.Threading;
using System.Threading.Tasks;

namespace RRBridal.StoreBilling.App.Services.Store;

public sealed class DaySessionGuard
{
    private readonly DaySessionService _daySessions;

    public DaySessionGuard(DaySessionService daySessions)
    {
        _daySessions = daySessions;
    }

    public async Task<string?> ValidatePostingAsync(
        string storeId,
        string businessDate,
        string posCounter,
        CancellationToken ct = default)
    {
        if (await _daySessions.IsDayLockedAsync(storeId, businessDate, posCounter, ct))
            return "This business day is closed. Transactions are locked.";

        if (!await _daySessions.IsDayOpenAsync(storeId, businessDate, posCounter, ct))
            return "Open the day before posting transactions.";

        return null;
    }

    public Task<string?> ValidatePostingTodayAsync(
        string storeId,
        string posCounter,
        CancellationToken ct = default) =>
        ValidatePostingAsync(storeId, DaySessionService.FormatBusinessDate(DateTime.Today), posCounter, ct);
}
