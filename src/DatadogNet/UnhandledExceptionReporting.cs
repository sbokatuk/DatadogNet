namespace DatadogNet;

/// <summary>
/// Reports exceptions nobody caught as RUM errors and log entries.
/// </summary>
/// <remarks>
/// A MAUI app gets this enabled by <c>UseDatadog</c> unless its options turn it off. Every other
/// host — a plain .NET Android or iOS app, a background service, a generic host using the
/// dependency-injection package — calls <see cref="Enable"/> once, right after
/// <see cref="Datadog.Initialize"/>.
/// <para>
/// This is not a replacement for <c>DatadogNet.CrashReporting</c>, and the two report different
/// things about the same failure. A crash reporter sees the process die and files a native stack;
/// this sees the managed exception on its way out, while it still has its .NET type, message and
/// managed frames — which is what makes the error searchable by exception type rather than by
/// memory address.
/// </para>
/// <para>
/// Hooks <see cref="AppDomain.UnhandledException"/>,
/// <see cref="TaskScheduler.UnobservedTaskException"/> (which never terminates the process and is
/// otherwise invisible), and each platform's own boundary — Android's
/// <c>UnhandledExceptionRaiser</c> and Apple's managed-exception marshalling — where a failure can
/// end the app without ever reaching the <c>AppDomain</c> hook. A failure seen by more than one
/// hook is reported once.
/// </para>
/// </remarks>
public static partial class UnhandledExceptionReporting
{
    private static readonly object Gate = new();

    /// <summary>
    /// Exceptions already reported, so one failure surfacing through two hooks — a marshalled
    /// UIKit-callback exception that then terminates the process, say — is one RUM error, not two.
    /// Weak on the exception, so tracking reported failures keeps none of them alive.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Exception, object> Reported = [];

    private static readonly object ReportedMarker = new();

    private static bool enabled;

    /// <summary>
    /// Starts reporting. Safe to call more than once; later calls do nothing.
    /// </summary>
    /// <remarks>
    /// Enable after <see cref="Datadog.Initialize"/> for the ordinary case. Enabling earlier is
    /// harmless — the hooks report through the same façade as everything else, so a failure before
    /// the SDK is up is dropped rather than thrown at.
    /// </remarks>
    public static void Enable()
    {
        lock (Gate)
        {
            if (enabled)
            {
                return;
            }

            enabled = true;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // AppDomain.UnhandledException is not the whole story on either platform: an exception
            // leaving a native callback can be marshalled or rethrown at the boundary and
            // terminate the app without ever reaching it. Each head adds the hook that sees those.
            EnablePlatformHooks();
        }
    }

    /// <summary>Adds the platform's own last-chance hook. Supplied per head.</summary>
    private static partial void EnablePlatformHooks();

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        // Nothing here waits for an upload. The process is about to end and the SDKs batch to disk,
        // so what this buys is the event surviving on disk to be uploaded on the next launch -
        // which is also how both native crash reporters work. Blocking on a flush would just delay
        // the crash.
        Report(exception, fatal: e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Deliberately not calling e.SetObserved(): marking it observed would change the app's own
        // behaviour, and instrumentation that alters what it measures is worse than none. On
        // current .NET this does not terminate the process either way.
        Report(e.Exception, fatal: false, unobserved: true);
    }

    private static void Report(Exception exception, bool fatal, bool unobserved = false)
    {
        try
        {
            lock (Gate)
            {
                if (Reported.TryGetValue(exception, out _))
                {
                    return;
                }

                Reported.Add(exception, ReportedMarker);
            }

            var attributes = new Dictionary<string, object?>
            {
                ["error.is_crash"] = fatal,
                ["error.unobserved_task"] = unobserved,
            };

            Datadog.Rum.AddError(exception, RumErrorSource.Source, attributes);
            Datadog.Logger.Critical(exception.Message, exception, attributes);
        }
        catch
        {
            // An exception handler that throws replaces the app's own crash with one from its
            // telemetry, and the original is then never reported at all. There is nowhere useful to
            // report a failure here to, since the thing that failed is the reporting.
        }
    }
}
