namespace DatadogNet.DeviceTests;

/// <summary>
/// Runs every check and reports a verdict the runner scripts can grep for.
/// </summary>
/// <remarks>
/// Shared by both heads, so the two platforms cannot disagree about what counts as a pass. The
/// reporting is a line per check plus one terminal <c>DATADOG_E2E_DONE</c>; both runner scripts
/// look for exactly that.
/// </remarks>
public static class TestRunner
{
    /// <summary>The line each runner script greps for.</summary>
    public const string DoneMarker = "DATADOG_E2E_DONE";

    /// <summary>
    /// Runs every check, reporting through <paramref name="report"/>.
    /// </summary>
    /// <param name="report">Writes one line to the platform log.</param>
    /// <param name="exit">
    /// Called with the process exit code once the verdict has been reported. iOS terminates so
    /// <c>simctl launch --console-pty</c> returns instead of hanging; Android does nothing, because
    /// the runner reads the verdict from logcat and killing the app would race the flush.
    /// </param>
    public static async Task RunAndReportAsync(Action<string> report, Action<int> exit)
    {
        SmokeTests.Reporter = message => report($"    {message}");

        var failures = 0;

        foreach (var test in SmokeTests.All)
        {
            try
            {
                await test.Execute();
                report($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;

                // The type as well as the message: half the interesting failures here are a native
                // call throwing something the façade did not expect, and the message alone rarely
                // says which side of the bridge it came from.
                report($"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");

                if (exception.StackTrace is { } stack)
                {
                    report(stack);
                }
            }
        }

        report(failures == 0
            ? $"{DoneMarker} PASS"
            : $"{DoneMarker} FAIL ({failures} failed)");

        Console.Out.Flush();

        exit(failures == 0 ? 0 : 1);
    }
}
