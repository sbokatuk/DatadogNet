using Microsoft.Maui.LifecycleEvents;

namespace DatadogNet.Maui;

public static partial class MauiAppBuilderExtensions
{
    /// <remarks>
    /// <c>FinishedLaunching</c> is the first point at which MAUI has created its
    /// <c>Application</c>, so it is the earliest moment page tracking can subscribe. Returning
    /// <see langword="true"/> is required: the delegate's value is the app delegate's own return,
    /// and <see langword="false"/> tells iOS the launch was not handled.
    /// </remarks>
    private static partial void AttachNavigationTracking(MauiAppBuilder builder) =>
        builder.ConfigureLifecycleEvents(events => events.AddiOS(ios =>
            ios.FinishedLaunching((_, _) =>
            {
                DatadogTracking.AttachToCurrentApplication();
                return true;
            })));
}
