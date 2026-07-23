using Foundation;
using UIKit;

namespace DatadogNet.DeviceTests;

/// <summary>
/// Host for the on-simulator checks. Runs every one on launch, reports the outcome to stdout -
/// which <c>simctl launch --console-pty</c> streams straight back to CI - and exits with a verdict
/// line the runner script greps for.
/// </summary>
public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register(nameof(AppDelegate))]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    /// <remarks>
    /// <paramref name="launchOptions"/> is nullable, which matters across target frameworks rather
    /// than just here: the iOS 26 SDK annotates the base member's parameter as nullable, so
    /// declaring it non-null is CS8765 on net10 while compiling clean on net8 and net9. Declaring it
    /// nullable is accepted by all three, because widening a parameter in an override always is.
    /// </remarks>
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // A window is not strictly needed for a headless run, but iOS terminates an app that never
        // presents one, which would look like a crash rather than a failing check.
        // UIColor.White, not SystemBackground: the app's deployment target is 12.2, matching the
        // packages, and SystemBackground is iOS 13. Nothing here looks at the window anyway.
        var root = new UIViewController();
        root.View!.BackgroundColor = UIColor.White;

        // UIScreen.MainScreen and this UIWindow constructor are obsoleted from iOS 26 in favour of
        // the UIWindowScene overloads, which do not exist below iOS 13 - and this app deliberately
        // deploys to 12.2, matching the packages. CA1422 is therefore correct and unactionable, so
        // it is suppressed here rather than project-wide: a version-compatibility warning anywhere
        // else in this harness is worth seeing.
#pragma warning disable CA1422
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = root };
#pragma warning restore CA1422
        Window.MakeKeyAndVisible();

        // On the main thread deliberately: the Datadog SDK instruments UIKit and DDRUMMonitor
        // asserts it is reached from the main thread. Nothing here blocks long enough to trip the
        // watchdog.
        _ = TestRunner.RunAndReportAsync(
            Console.WriteLine,
            exitCode => Environment.Exit(exitCode));

        return true;
    }
}
