using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class BillListCsvExporter
{
    private static readonly string[] Headers =
    [
        "Bill no",
        "Posted (local)",
        "Bill date",
        "Counter",
        "Customer",
        "Mobile",
        "Qty",
        "Payable",
        "Cash",
        "Card",
        "UPI",
        "Credit note",
        "Returned",
        "Return no",
        "Adjustment",
        "Adjustment no",
        "Sync",
    ];

    public static void ExportToFile(string filePath, IReadOnlyList<StoreBillListRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers.Select(Escape)));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(row.BillNo),
                Escape(row.PostedAtLocal),
                Escape(row.BillDate),
                Escape(row.CounterDisplay),
                Escape(row.CustomerName),
                Escape(row.CustomerPhone),
                Escape(row.TotalQty.ToString("0.##", CultureInfo.InvariantCulture)),
                Escape(row.Payable.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(row.CashAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(row.CardAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(row.UpiAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(row.CreditNoteAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                Escape(row.ReturnedDisplay),
                Escape(row.ReturnNo),
                Escape(row.AdjustmentDisplay),
                Escape(row.AdjustmentNo),
                Escape(row.SyncStatus)));
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string? value)
    {
        var s = value ?? "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
