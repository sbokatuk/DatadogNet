using Microsoft.Maui.LifecycleEvents;

namespace DatadogNet.Maui;

public static partial class MauiAppBuilderExtensions
{
    /// <remarks>
    /// Byte-for-byte the Platforms/iOS implementation, and that is not laziness: Mac Catalyst is
    /// UIKit, so MAUI routes its lifecycle through the *iOS* lifecycle builder - there is no
    /// <c>AddMacCatalyst</c> - and <c>FinishedLaunching</c> is the same earliest moment page
    /// tracking can subscribe. It cannot be the same file, though. Everywhere else in this
    /// repository the Catalyst head simply compiles Platforms/iOS (see Datadog.Facade.props), but
    /// this project is UseMaui+SingleProject, and MAUI's single-project targets remove
    /// Platforms/iOS/** from every non-iOS head after our own item groups have run.
    /// </remarks>
    private static partial void AttachNavigationTracking(MauiAppBuilder builder) =>
        builder.ConfigureLifecycleEvents(events => events.AddiOS(ios =>
            ios.FinishedLaunching((_, _) =>
            {
                DatadogTracking.AttachToCurrentApplication();
                return true;
            })));
}
