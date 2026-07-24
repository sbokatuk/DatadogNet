namespace DatadogNet.Maui;

/// <summary>
/// Reports exceptions nobody caught as RUM errors and log entries.
/// </summary>
/// <remarks>
/// Enabled by <see cref="MauiAppBuilderExtensions.UseDatadog"/> unless
/// <see cref="DatadogMauiOptions.TrackUnhandledExceptions"/> is off.
/// <para>
/// This is not a replacement for <c>DatadogNet.CrashReporting</c>, and the two report different
/// things about the same failure. A crash reporter sees the process die and files a native stack;
/// this sees the managed exception on its way out, while it still has its .NET type, message and
/// managed frames — which is what makes the error searchable by exception type rather than by
/// memory address.
/// </para>
/// </remarks>
internal static partial class UnhandledExceptionReporting
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

    internal static void Enable()
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
