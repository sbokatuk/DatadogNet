using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DatadogNet;

/// <summary>
/// Sends <c>Microsoft.Extensions.Logging</c> output to Datadog.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Adds the Datadog logging provider.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="loggerOptions">
    /// How the Datadog loggers behind the provider are configured. <see langword="null"/> uses the
    /// SDK's own defaults. The logger name is always the <c>ILogger</c> category.
    /// </param>
    /// <param name="minimumLogLevel">
    /// The lowest level forwarded to Datadog. Defaults to <see cref="LogLevel.Information"/>,
    /// separate from the logging framework's own filters and applied on top of them: debug and
    /// trace logs are numerous, and each one is an event Datadog bills for.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// An app that already logs through <c>ILogger&lt;T&gt;</c> gets its logs into Datadog with no
    /// call-site changes, each entry tagged with its category and correlated with the RUM view it
    /// was written in. In a MAUI app <c>UseDatadog</c> calls this for you; call it directly from
    /// any other <c>Microsoft.Extensions.Logging</c> host.
    /// </remarks>
    public static ILoggingBuilder AddDatadog(
        this ILoggingBuilder builder,
        LoggerOptions? loggerOptions = null,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The two-type-argument overload, not ServiceDescriptor.Singleton<ILoggerProvider>(factory):
        // TryAddEnumerable de-duplicates on the descriptor's implementation *type*, and a
        // factory-only descriptor has none - so it throws "Implementation type cannot be inferred"
        // rather than registering anything.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, DatadogLoggerProvider>(
                _ => new DatadogLoggerProvider(loggerOptions, minimumLogLevel)));

        return builder;
    }
}
