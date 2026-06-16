using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RRBridal.StoreBilling.App.Services.Store;

public static class DayCloseCsvExporter
{
    public static void ExportToFile(string filePath, DayCloseReportData data)
    {
        File.WriteAllText(filePath, BuildContent(data), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static string BuildContent(DayCloseReportData data)
    {
        var sb = new StringBuilder();

        AppendKeyValueSection(sb, "METADATA", DayCloseReportSectionBuilder.BuildMetadataRows(data));
        AppendKeyValueSection(sb, "SUMMARY", DayCloseReportSectionBuilder.BuildSummaryRows(data));

        foreach (var section in DayCloseReportSectionBuilder.BuildAllDetailSections(data))
            AppendTableSection(sb, section.Name, section.Headers, section.Rows);

        return sb.ToString();
    }

    private static void AppendKeyValueSection(StringBuilder sb, string name, IReadOnlyList<(string Label, string Value)> rows)
    {
        sb.AppendLine($"--- {name} ---");
        foreach (var (label, value) in rows)
            sb.AppendLine($"{Escape(label)},{Escape(value)}");
        sb.AppendLine();
    }

    private static void AppendTableSection(
        StringBuilder sb,
        string name,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        sb.AppendLine($"--- {name} ---");
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        sb.AppendLine();
    }

    private static string Escape(string? value)
    {
        var s = value ?? "";
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
