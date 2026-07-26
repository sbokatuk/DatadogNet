namespace DatadogNet;

/// <summary>
/// Reports crashes to Datadog as RUM errors and logs, on the next launch.
/// </summary>
/// <remarks>
/// A separate package because it installs a signal handler, which is not something a package called
/// <c>DatadogNet</c> should do on your behalf.
/// <code>
/// Datadog.Initialize (configuration);
/// CrashReporting.Enable ();
/// </code>
/// <b>After</b> <see cref="Datadog.Initialize"/>, never before: the crash reporter attaches to the
/// RUM and Logs features to file what it finds, and enabling it first is a silent no-op on both
/// platforms.
/// <para>
/// What it adds differs by platform, and the difference is worth knowing:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>iOS</b> — all of crash reporting. dd-sdk-ios has nothing built in; <c>DatadogCrashReporting</c>
/// is a separate framework carrying KSCrash (3.x swapped it in for the 2.x line's PLCrashReporter).
/// Without this package an iOS app reports no crashes.
/// </item>
/// <item>
/// <b>Android</b> — native (NDK) crashes only. JVM-level crashes, which is where an unhandled .NET
/// exception in a MAUI app arrives, are already covered by
/// <see cref="DatadogConfiguration.CrashReportsEnabled"/> in the core package. This adds crashes in
/// native code beneath it.
/// </item>
/// </list>
/// <para>
/// Symbolication is a separate step in both cases: upload your dSYMs on iOS and your native symbols
/// on Android, or the stack traces arrive as addresses.
/// </para>
/// </remarks>
public static partial class CrashReporting
{
    /// <summary>
    /// Turns crash reporting on.
    /// </summary>
    /// <remarks>
    /// Does nothing on a platform with no Datadog support, and — like everything else in this
    /// façade — does not throw when the SDK is not initialised. Calling it twice is harmless.
    /// </remarks>
    public static void Enable() => PlatformEnable();

    private static partial void PlatformEnable();
}
