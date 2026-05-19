using System;

namespace RRBridal.StoreBilling.App.Services.Sync;

public sealed class SyncRunResult
{
    public bool SkippedBecauseBusy { get; init; }

    public bool Succeeded { get; init; }

    public string Message { get; init; } = "";

    public static SyncRunResult Skipped() => new() { SkippedBecauseBusy = true, Message = "Sync already in progress." };

    public static SyncRunResult Ok(string message) => new() { Succeeded = true, Message = message };

    public static SyncRunResult Failed(string message) => new() { Succeeded = false, Message = message };
}
