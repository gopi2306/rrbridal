using System;
using System.Globalization;

namespace RRBridal.StoreBilling.App.Services.Store;

/// <summary>
/// Converts the local dashboard date filters (business date / date range) used by the
/// offline aggregation services into the <c>period</c>/<c>from</c>/<c>to</c> query shape
/// expected by the central <c>/api/dashboard/store/sales/*</c> endpoints.
/// </summary>
internal static class OnlineSalesPeriodMapper
{
    public static (string Period, string? From, string? To) Resolve(
        DateTime? businessDate,
        bool useDateRange,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        if (useDateRange)
        {
            var from = (dateFrom ?? dateTo)?.Date;
            var to = (dateTo ?? dateFrom)?.Date;
            if (from.HasValue && to.HasValue && from > to)
                (from, to) = (to, from);

            return ("custom", FormatYmd(from), FormatYmd(to));
        }

        var date = (businessDate ?? DateTime.Today).Date;
        if (date == DateTime.Today)
            return ("today", null, null);

        var ymd = FormatYmd(date);
        return ("custom", ymd, ymd);
    }

    private static string? FormatYmd(DateTime? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
