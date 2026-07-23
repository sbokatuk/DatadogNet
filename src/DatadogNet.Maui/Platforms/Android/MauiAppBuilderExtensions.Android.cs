using Microsoft.Maui.LifecycleEvents;

namespace DatadogNet.Maui;

public static partial class MauiAppBuilderExtensions
{
    /// <remarks>
    /// <c>OnPostCreate</c> on the activity rather than <c>OnApplicationCreate</c>: MAUI creates its
    /// <c>Application</c> object while the first activity is being created, so at application-create
    /// time <c>Application.Current</c> is still null. It fires again for every activity, and on
    /// every configuration change that recreates one - which is why
    /// <see cref="DatadogTracking.Attach"/> is idempotent per application instance rather than
    /// merely idempotent.
    /// </remarks>
    private static partial void AttachNavigationTracking(MauiAppBuilder builder) =>
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
            android.OnPostCreate((_, _) => DatadogTracking.AttachToCurrentApplication())));
}
