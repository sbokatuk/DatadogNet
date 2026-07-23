using DatadogCore;
using DatadogLogs;

namespace DatadogNet;

/// <summary>Logs over <c>DDLogs</c> and <c>DDLogger</c>.</summary>
internal sealed class IosLogs : IDatadogLogs
{
    public bool IsEnabled => Datadog.Configuration?.Logs is not null;

    public IDatadogLogger CreateLogger(LoggerOptions? options = null)
    {
        options ??= new LoggerOptions();

        // DDLogger.Create is DatadogNet.iOS's convenience form. The generated type has only the
        // designated initializer, which takes all eight settings positionally with no defaults.
        var native = DDLogger.Create(
            name: options.Name,
            service: options.Service,
            networkInfoEnabled: options.NetworkInfoEnabled,
            bundleWithRumEnabled: options.BundleWithRumEnabled,
            bundleWithTraceEnabled: options.BundleWithTraceEnabled,
            remoteSampleRate: options.RemoteSampleRate,
            remoteLogThreshold: ToNative(options.RemoteLogThreshold),
            printLogsToConsole: options.PrintToConsole);

        return new IosLogger(native);
    }

    public void AddAttribute(string key, object? value) =>
        DDLogs.AddAttributeForKey(key, NativeAttributes.Single(key, value));

    public void RemoveAttribute(string key) => DDLogs.RemoveAttributeForKey(key);

    internal static DDLogLevel ToNative(DatadogLogLevel level) => level switch
    {
        DatadogLogLevel.Info => DDLogLevel.Info,
        DatadogLogLevel.Notice => DDLogLevel.Notice,
        DatadogLogLevel.Warn => DDLogLevel.Warn,
        DatadogLogLevel.Error => DDLogLevel.Error,
        DatadogLogLevel.Critical => DDLogLevel.Critical,
        _ => DDLogLevel.Debug,
    };
}

/// <summary>A logger over <c>DDLogger</c>.</summary>
internal sealed class IosLogger(DDLogger native) : IDatadogLogger
{
    public void Log(
        DatadogLogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The level-as-argument overload from DatadogNet.iOS's additions, which folds the exception
        // into Datadog's reserved error.* attributes. The bound API is six methods per level, each
        // taking an NSError a managed exception is not.
        native.Log(IosLogs.ToNative(level), message, exception, attributes);
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
        native.AddAttributeForKey(key, NativeAttributes.Single(key, value));

    public void RemoveAttribute(string key) => native.RemoveAttributeForKey(key);

    public void AddTag(string key, string value) => native.AddTagWithKey(key, value);

    public void RemoveTagsWithKey(string key) => native.RemoveTagWithKey(key);
}
