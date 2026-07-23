using Com.Datadog.Android;
using Com.Datadog.Android.Log;

using NativeLogs = Com.Datadog.Android.Log.Logs;

namespace DatadogNet;

/// <summary>Logs over <c>Logs</c> and <c>Logger</c>.</summary>
internal sealed class AndroidLogs : IDatadogLogs
{
    public bool IsEnabled => NativeLogs.IsEnabled;

    public IDatadogLogger CreateLogger(LoggerOptions? options = null)
    {
        options ??= new LoggerOptions();

        var builder = new Logger.Builder()
            .SetNetworkInfoEnabled(options.NetworkInfoEnabled)
            .SetBundleWithRumEnabled(options.BundleWithRumEnabled)
            .SetBundleWithTraceEnabled(options.BundleWithTraceEnabled)
            .SetRemoteSampleRate(options.RemoteSampleRate)
            .SetRemoteLogThreshold((int)ToNative(options.RemoteLogThreshold))
            .SetLogcatLogsEnabled(options.PrintToConsole);

        if (options.Name is { Length: > 0 } name)
        {
            builder.SetName(name);
        }

        if (options.Service is { Length: > 0 } service)
        {
            builder.SetService(service);
        }

        return new AndroidLogger(builder.Build());
    }

    public void AddAttribute(string key, object? value) =>
        NativeLogs.AddAttribute(key, DatadogAttributes.ToJava(value, key));

    public void RemoveAttribute(string key) => NativeLogs.RemoveAttribute(key);

    /// <summary>
    /// Maps a Datadog level onto the <c>android.util.Log</c> priority the SDK records.
    /// </summary>
    /// <remarks>
    /// dd-sdk-android takes the priority as a bare <see cref="int"/>, because Kotlin passes
    /// <c>android.util.Log</c>'s constants straight through, and it has no <c>Notice</c> — Datadog
    /// has six levels and Android's log has five that matter here. <c>Notice</c> therefore maps to
    /// <c>INFO</c>, which is how dd-sdk-android's own <c>Logger</c> reports it: the alternative,
    /// promoting it to <c>WARN</c>, would put ordinary events in a warning feed.
    /// </remarks>
    internal static DatadogLogLevelPriority ToNative(DatadogLogLevel level) => level switch
    {
        DatadogLogLevel.Info => DatadogLogLevelPriority.Info,
        DatadogLogLevel.Notice => DatadogLogLevelPriority.Info,
        DatadogLogLevel.Warn => DatadogLogLevelPriority.Warn,
        DatadogLogLevel.Error => DatadogLogLevelPriority.Error,
        DatadogLogLevel.Critical => DatadogLogLevelPriority.Critical,
        _ => DatadogLogLevelPriority.Debug,
    };
}

/// <summary>The <c>android.util.Log</c> priorities dd-sdk-android records levels as.</summary>
internal enum DatadogLogLevelPriority
{
    Debug = 3,
    Info = 4,
    Warn = 5,
    Error = 6,

    /// <summary><c>android.util.Log.ASSERT</c>, which Datadog reports as CRITICAL.</summary>
    Critical = 7,
}

/// <summary>A logger over <c>Logger</c>.</summary>
internal sealed class AndroidLogger(Logger native) : IDatadogLogger
{
    public void Log(
        DatadogLogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The five-string overload rather than the Throwable one: a managed exception is not a
        // java.lang.Throwable, and passing its type, message and stack as strings is what gets all
        // three to Datadog as error.kind, error.message and error.stack.
        native.Log(
            (int)AndroidLogs.ToNative(level),
            message,
            exception?.GetType().FullName!,
            exception?.Message!,
            exception?.ToString()!,
            DatadogAttributes.From(attributes));
    }

    public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Debug, message, exception, attributes);

    public void Info(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Info, message, exception, attributes);

    public void Notice(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Notice, message, exception, attributes);

    public void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Warn, message, exception, attributes);

    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Error, message, exception, attributes);

    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Log(DatadogLogLevel.Critical, message, exception, attributes);

    public void AddAttribute(string key, object? value) =>
        native.AddAttribute(key, value);

    public void RemoveAttribute(string key) => native.RemoveAttribute(key);

    public void AddTag(string key, string value) => native.AddTag(key, value);

    public void RemoveTagsWithKey(string key) => native.RemoveTagsWithKey(key);
}
