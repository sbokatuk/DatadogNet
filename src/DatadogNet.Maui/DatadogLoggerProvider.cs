using Microsoft.Extensions.Logging;

namespace DatadogNet.Maui;

/// <summary>
/// Sends <c>Microsoft.Extensions.Logging</c> output to Datadog.
/// </summary>
/// <remarks>
/// One Datadog logger per category, so an entry's <c>logger.name</c> in Datadog is the
/// <c>ILogger&lt;T&gt;</c> category it was written through — <c>MyApp.Services.OrderService</c> —
/// which is what makes the Logs UI's grouping match the code's.
/// </remarks>
internal sealed class DatadogLoggerProvider(DatadogMauiOptions options) : ILoggerProvider
{
    private readonly Dictionary<string, ILogger> loggers = [];

    private readonly object gate = new();

    public ILogger CreateLogger(string categoryName)
    {
        lock (gate)
        {
            if (loggers.TryGetValue(categoryName, out var existing))
            {
                return existing;
            }

            var created = new DatadogCategoryLogger(
                Datadog.Logs.CreateLogger(WithName(options.Logger, categoryName)),
                options.MinimumLogLevel);

            loggers[categoryName] = created;
            return created;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            loggers.Clear();
        }
    }

    /// <summary>Copies the app's logger options, replacing the name with the category.</summary>
    private static LoggerOptions WithName(LoggerOptions? source, string name)
    {
        source ??= new LoggerOptions();

        return new LoggerOptions
        {
            Name = name,
            Service = source.Service,
            NetworkInfoEnabled = source.NetworkInfoEnabled,
            BundleWithRumEnabled = source.BundleWithRumEnabled,
            BundleWithTraceEnabled = source.BundleWithTraceEnabled,
            RemoteSampleRate = source.RemoteSampleRate,
            RemoteLogThreshold = source.RemoteLogThreshold,
            PrintToConsole = source.PrintToConsole,
        };
    }
}

/// <summary>The <c>ILogger</c> for one category.</summary>
internal sealed class DatadogCategoryLogger(IDatadogLogger logger, LogLevel minimum) : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        var attributes = eventId.Id == 0 && eventId.Name is null
            ? null
            : new Dictionary<string, object?>
            {
                ["logger.event_id"] = eventId.Id,
                ["logger.event_name"] = eventId.Name,
            };

        logger.Log(ToDatadogLevel(logLevel), formatter(state, exception), exception, attributes);
    }

    /// <summary>
    /// Maps the six <c>Microsoft.Extensions.Logging</c> levels onto Datadog's six.
    /// </summary>
    /// <remarks>
    /// Not a one-for-one match, and the two ends are where it shows. <c>Trace</c> and
    /// <c>Debug</c> both become <see cref="DatadogLogLevel.Debug"/>, since Datadog has nothing
    /// below it; <c>Critical</c> becomes <see cref="DatadogLogLevel.Critical"/>. Nothing produces
    /// <see cref="DatadogLogLevel.Notice"/>, which has no <c>ILogger</c> counterpart — it is
    /// reachable through <see cref="IDatadogLogger"/> directly.
    /// </remarks>
    private static DatadogLogLevel ToDatadogLevel(LogLevel level) => level switch
    {
        LogLevel.Information => DatadogLogLevel.Info,
        LogLevel.Warning => DatadogLogLevel.Warn,
        LogLevel.Error => DatadogLogLevel.Error,
        LogLevel.Critical => DatadogLogLevel.Critical,
        _ => DatadogLogLevel.Debug,
    };

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
