using Microsoft.Extensions.Logging;

namespace DatadogNet;

/// <summary>
/// Sends <c>Microsoft.Extensions.Logging</c> output to Datadog.
/// </summary>
/// <remarks>
/// One Datadog logger per category, so an entry's <c>logger.name</c> in Datadog is the
/// <c>ILogger&lt;T&gt;</c> category it was written through — <c>MyApp.Services.OrderService</c> —
/// which is what makes the Logs UI's grouping match the code's.
/// <para>
/// The bridge carries the structure <c>ILogger</c> already has, not just the rendered text:
/// message-template values (<c>LogInformation("Order {OrderId} placed", id)</c> produces an
/// <c>OrderId</c> attribute), scope values via <see cref="ISupportExternalScope"/>, the template
/// itself as <c>logger.template</c>, and the event id as <c>logger.event_id</c>/<c>_name</c>.
/// Faceted attributes are the reason logs go to Datadog rather than a file; flattening them to
/// text at the last step would throw away exactly the part the Logs UI queries on.
/// </para>
/// </remarks>
internal sealed class DatadogLoggerProvider(
    IDatadogLogs? logs,
    LoggerOptions? options,
    LogLevel minimumLogLevel) : ILoggerProvider, ISupportExternalScope
{
    private readonly Dictionary<string, ILogger> loggers = [];

    private readonly object gate = new();

    /// <summary>
    /// The scope stack shared by every provider in the factory, handed over by the logging
    /// infrastructure. <see langword="null"/> when the provider is used outside a factory.
    /// </summary>
    internal IExternalScopeProvider? ScopeProvider { get; private set; }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => ScopeProvider = scopeProvider;

    public ILogger CreateLogger(string categoryName)
    {
        lock (gate)
        {
            if (loggers.TryGetValue(categoryName, out var existing))
            {
                return existing;
            }

            // The injected IDatadogLogs when the host registered one (which is also what lets a
            // test host observe this provider through a fake); the static's otherwise. Loggers
            // created before Initialize come alive on their own — see IDatadogLogger.
            var created = new DatadogCategoryLogger(
                (logs ?? Datadog.Logs).CreateLogger(WithName(options, categoryName)),
                minimumLogLevel,
                this);

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
internal sealed class DatadogCategoryLogger(
    IDatadogLogger logger,
    LogLevel minimum,
    DatadogLoggerProvider provider) : ILogger
{
    /// <summary>The key <c>FormattedLogValues</c> stores the message template under.</summary>
    private const string OriginalFormatKey = "{OriginalFormat}";

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        provider.ScopeProvider?.Push(state) ?? NullScope.Instance;

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

        var message = formatter(state, exception);
        Dictionary<string, object?>? attributes = null;

        void Set(string key, object? value)
        {
            attributes ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            attributes[key] = value;
        }

        // Scopes first, outer to inner, so an inner scope's value wins over an outer's — the same
        // shadowing every other structured sink applies. Structured scope values become attributes;
        // unstructured ones stay display text, collected under logger.scope in nesting order.
        if (provider.ScopeProvider is { } scopes)
        {
            List<object?>? plainScopes = null;

            scopes.ForEachScope(
                (scope, _) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            if (key != OriginalFormatKey)
                            {
                                Set(key, value);
                            }
                        }
                    }
                    else if (scope is not null)
                    {
                        (plainScopes ??= []).Add(scope.ToString());
                    }
                },
                state);

            if (plainScopes is not null)
            {
                Set("logger.scope", plainScopes);
            }
        }

        // Then the entry's own template values, which shadow anything a scope set: the call site
        // is more specific than its surroundings.
        string? template = null;

        if (state is IEnumerable<KeyValuePair<string, object?>> stateValues)
        {
            foreach (var (key, value) in stateValues)
            {
                if (key == OriginalFormatKey)
                {
                    template = value as string;
                }
                else
                {
                    Set(key, value);
                }
            }
        }

        // The template only earns an attribute when it differs from the rendered message — that is
        // when it groups: every "Order {OrderId} placed" entry shares one logger.template however
        // many order ids there are.
        if (template is not null && template != message)
        {
            Set("logger.template", template);
        }

        if (eventId.Id != 0 || eventId.Name is not null)
        {
            Set("logger.event_id", eventId.Id);
            Set("logger.event_name", eventId.Name);
        }

        logger.Log(ToDatadogLevel(logLevel), message, exception, attributes);
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
