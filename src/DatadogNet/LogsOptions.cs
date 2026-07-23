namespace DatadogNet;

/// <summary>
/// Log collection.
/// </summary>
/// <remarks>
/// Assign to <see cref="DatadogConfiguration.Logs"/> to enable Logs. Loggers are then created with
/// <see cref="IDatadogLogs.CreateLogger"/>, or taken from <see cref="Datadog.Logger"/>.
/// </remarks>
public sealed class LogsOptions
{
    /// <summary>Send logs somewhere other than the site's intake. For a proxy or a local test.</summary>
    public Uri? CustomEndpoint { get; init; }

    /// <summary>
    /// Reaches the native Logs configuration before Logs is enabled.
    /// </summary>
    /// <remarks>
    /// The argument is <c>Com.Datadog.Android.Log.LogsConfiguration.Builder</c> on Android and
    /// <c>DatadogObjc.DDLogsConfiguration</c> on iOS. This is where the log event mapper lives —
    /// the supported way to rewrite or drop a log on the device before upload — which can only be
    /// set here.
    /// </remarks>
    public Action<object>? ConfigureNative { get; init; }
}

/// <summary>
/// The severity of a log entry.
/// </summary>
/// <remarks>
/// These are Datadog's own six levels, which is what both SDKs record. They are not the platform
/// log levels: Android's <c>Log.ASSERT</c> is reported to Datadog as <see cref="Critical"/>, and
/// <c>Log.VERBOSE</c> as <see cref="Debug"/> — iOS has no <c>Verbose</c>, so a shared
/// <c>Verbose</c> would be a level that silently means something different on each platform.
/// </remarks>
public enum DatadogLogLevel
{
    /// <summary>Detail only useful while debugging.</summary>
    Debug,

    /// <summary>Ordinary progress.</summary>
    Info,

    /// <summary>Notable, but not a problem.</summary>
    Notice,

    /// <summary>A problem the app recovered from.</summary>
    Warn,

    /// <summary>A problem it did not.</summary>
    Error,

    /// <summary>The app cannot continue.</summary>
    Critical,
}

/// <summary>
/// How one logger behaves, distinct from whether Logs is enabled at all.
/// </summary>
/// <remarks>
/// Every property carries the native SDK's own default, so <c>new LoggerOptions()</c> is the same
/// logger both SDKs give you for asking with no arguments.
/// </remarks>
public sealed class LoggerOptions
{
    /// <summary>The logger name, reported as the event's <c>logger.name</c>.</summary>
    public string? Name { get; init; }

    /// <summary>The service the logs belong to. Defaults to the SDK's service.</summary>
    public string? Service { get; init; }

    /// <summary>Attach network connectivity information to each log.</summary>
    public bool NetworkInfoEnabled { get; init; }

    /// <summary>
    /// Correlate each log with the RUM view active when it was written. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// This is what lets a log line be opened straight from a session replay, and a RUM error be
    /// read next to the logs around it. Worth leaving on.
    /// </remarks>
    public bool BundleWithRumEnabled { get; init; } = true;

    /// <summary>Correlate each log with the active span. Defaults to <see langword="true"/>.</summary>
    public bool BundleWithTraceEnabled { get; init; } = true;

    /// <summary>Percentage of logs sent to Datadog, 0 to 100. Defaults to 100.</summary>
    public float RemoteSampleRate { get; init; } = 100;

    /// <summary>The lowest level actually uploaded. Defaults to <see cref="DatadogLogLevel.Debug"/>.</summary>
    public DatadogLogLevel RemoteLogThreshold { get; init; } = DatadogLogLevel.Debug;

    /// <summary>
    /// Also write each log to the platform console — logcat on Android, the Xcode console on iOS.
    /// </summary>
    public bool PrintToConsole { get; init; }
}
