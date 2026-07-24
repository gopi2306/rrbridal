using System;
using System.Windows;
using RRBridal.StoreBilling.App.Services.Ui;

namespace RRBridal.StoreBilling.App.Services;

public static class CounterConfigValidator
{
    public static void WarnIfDefaultDevice(StoreContext ctx)
    {
        if (!ctx.UsesDefaultDeviceId)
            return;

        AppDialog.Show(
            "DEVICE_ID is still the default (device-001).\n\n" +
            "For multi-counter deployment, set a unique DEVICE_ID and POS_COUNTER in the .env file beside this app " +
            "(see deploy/env.counter-01.example).\n\n" +
            $"Current till: {ctx.DisplayLabel}",
            "Counter configuration",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
