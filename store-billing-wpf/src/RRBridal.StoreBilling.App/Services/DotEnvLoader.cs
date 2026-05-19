using System;
using System.Collections.Generic;
using System.IO;

namespace RRBridal.StoreBilling.App.Services;

public static class DotEnvLoader
{
    public static void Load()
    {
        foreach (var path in CandidatePaths())
        {
            if (File.Exists(path))
            {
                LoadFile(path);
                return;
            }
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        foreach (var path in WalkUp(Environment.CurrentDirectory))
            yield return Path.Combine(path, ".env");

        foreach (var path in WalkUp(AppContext.BaseDirectory))
            yield return Path.Combine(path, ".env");
    }

    private static IEnumerable<string> WalkUp(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line["export ".Length..].TrimStart();

            var equalsAt = line.IndexOf('=');
            if (equalsAt <= 0)
                continue;

            var key = line[..equalsAt].Trim();
            var value = line[(equalsAt + 1)..].Trim();
            if (key.Length == 0)
                continue;

            // .env beside the app wins over pre-existing machine env (e.g. stale SYNC_INTERVAL_MINUTES).
            Environment.SetEnvironmentVariable(key, Unquote(value));
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                value = value[1..^1];
        }

        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal);
    }
}
