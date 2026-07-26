namespace DatadogNet;

/// <summary>
/// The implementation compiled into the platform-neutral <c>net9.0</c> and <c>net10.0</c>
/// assemblies, where there is no Datadog SDK to call.
/// </summary>
/// <remarks>
/// Reached by a MAUI app's Windows head, and by a unit test running against the library outside a
/// platform head. Every member is a no-op so that shared code needs no conditionals: a view-model
/// that reports a RUM view runs unchanged in a test, and a Windows head links without a single
/// <c>#if</c>.
/// <para>
/// Deliberately silent rather than throwing <see cref="PlatformNotSupportedException"/>. The whole
/// point of the neutral asset is that the same code path runs everywhere; a throwing stub would
/// mean every call site needed the guard the stub exists to avoid.
/// </para>
/// <para>
/// Mac Catalyst is <b>not</b> here: the <c>net*-maccatalyst</c> heads compile the iOS
/// implementation against the <c>DatadogNet.Mac</c> bindings, whose xcframeworks are built for
/// Catalyst from the same dd-sdk-ios sources. Catalyst apps get the real SDK, not this stub.
/// </para>
/// </remarks>
public static partial class Datadog
{
    private static partial bool PlatformIsSupported() => false;

    private static partial void PlatformInitialize(DatadogConfiguration configuration)
    {
    }

    private static partial void PlatformSetTrackingConsent(TrackingConsent consent)
    {
    }

    private static partial DatadogVerbosity PlatformGetVerbosity() => DatadogVerbosity.None;

    private static partial void PlatformSetVerbosity(DatadogVerbosity verbosity)
    {
    }

    private static partial void PlatformSetUser(
        string id,
        string? name,
        string? email,
        IReadOnlyDictionary<string, object?>? extraInfo)
    {
    }

    private static partial void PlatformAddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
    {
    }

    private static partial void PlatformClearUser()
    {
    }

    private static partial void PlatformSetAccount(
        string id,
        string? name,
        IReadOnlyDictionary<string, object?>? extraInfo)
    {
    }

    private static partial void PlatformAddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
    {
    }

    private static partial void PlatformClearAccount()
    {
    }

    private static partial void PlatformClearAllData()
    {
    }

    private static partial void PlatformStop()
    {
    }

    private static partial IRumMonitor CreateRum() => new NoOpRumMonitor();

    private static partial IDatadogLogs CreateLogs() => new NoOpLogs();

    private static partial IDatadogTracer CreateTracer() => new NoOpTracer();

    private static partial ISessionReplay CreateSessionReplay() => new NoOpSessionReplay();
}
