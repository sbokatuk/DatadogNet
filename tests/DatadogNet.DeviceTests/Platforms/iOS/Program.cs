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

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // A window is not strictly needed for a headless run, but iOS terminates an app that never
        // presents one, which would look like a crash rather than a failing check.
        // UIColor.White, not SystemBackground: the app's deployment target is 12.2, matching the
        // packages, and SystemBackground is iOS 13. Nothing here looks at the window anyway.
        var root = new UIViewController();
        root.View!.BackgroundColor = UIColor.White;

        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = root };
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
