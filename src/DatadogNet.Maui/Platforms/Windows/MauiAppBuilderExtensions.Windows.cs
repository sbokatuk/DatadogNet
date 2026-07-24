using Microsoft.Maui.LifecycleEvents;

namespace DatadogNet.Maui;

public static partial class MauiAppBuilderExtensions
{
    /// <remarks>
    /// The Windows head exists so a multi-headed app can reference this package unconditionally;
    /// every Datadog call it forwards to lands in the core package's documented no-op neutral
    /// implementation. Page tracking is still attached - it costs nothing, and the day Datadog
    /// supports Windows the views appear without the app changing.
    /// </remarks>
    private static partial void AttachNavigationTracking(MauiAppBuilder builder) =>
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows =>
            windows.OnLaunched((_, _) => DatadogTracking.AttachToCurrentApplication())));
}
