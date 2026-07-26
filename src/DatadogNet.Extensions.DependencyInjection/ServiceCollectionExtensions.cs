using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DatadogNet;

/// <summary>
/// Registers the Datadog services for constructor injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Initialises Datadog and registers its services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">What to start, and how.</param>
    /// <param name="loggerOptions">
    /// How the <see cref="IDatadogLogger"/> the app resolves is configured.
    /// <see langword="null"/> uses the SDK's own defaults.
    /// </param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <remarks>
    /// <code>
    /// builder.Services.AddDatadog (new DatadogConfiguration {
    ///     ClientToken     = "…",
    ///     Env             = "production",
    ///     TrackingConsent = TrackingConsent.Granted,
    ///     Rum             = new RumOptions { ApplicationId = "…" },
    ///     Logs            = new LogsOptions (),
    /// });
    /// </code>
    /// The SDK is initialised <b>during this call</b> rather than when the host starts — the same
    /// choice <c>UseDatadog</c> makes in a MAUI app, and for the same reason: crash reporting only
    /// covers what happens after the SDK is up, and startup failures are the ones worth catching.
    /// An app that initialises the SDK even earlier — an Android <c>Application</c> subclass, say —
    /// calls <see cref="AddDatadog(IServiceCollection, LoggerOptions?)"/> instead, which registers
    /// without initialising.
    /// </remarks>
    public static IServiceCollection AddDatadog(
        this IServiceCollection services,
        DatadogConfiguration configuration,
        LoggerOptions? loggerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        Datadog.Initialize(configuration);

        return services.AddDatadog(loggerOptions);
    }

    /// <summary>
    /// Initialises Datadog from configuration and registers its services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The section holding the Datadog settings — typically
    /// <c>builder.Configuration.GetSection("Datadog")</c>. Bound by
    /// <see cref="DatadogConfigurationBinder"/>; see it for the key shape.
    /// </param>
    /// <param name="loggerOptions">
    /// How the <see cref="IDatadogLogger"/> the app resolves is configured.
    /// <see langword="null"/> uses the SDK's own defaults.
    /// </param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <remarks>
    /// The <c>appsettings.json</c> path for a generic host:
    /// <code>
    /// builder.Services.AddDatadog (builder.Configuration.GetSection ("Datadog"));
    /// </code>
    /// A malformed section throws here, during startup, with the full configuration path in the
    /// message — the same stance <see cref="Datadog.Initialize"/> takes on a malformed
    /// configuration object, and for the same reason: a client token that is silently empty means
    /// every event vanishes with nothing ever saying why. Settings configuration cannot express —
    /// the <c>ConfigureNative</c> hooks — need the
    /// <see cref="AddDatadog(IServiceCollection, DatadogConfiguration, LoggerOptions?)"/> overload
    /// instead.
    /// </remarks>
    public static IServiceCollection AddDatadog(
        this IServiceCollection services,
        IConfiguration configuration,
        LoggerOptions? loggerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddDatadog(DatadogConfigurationBinder.Bind(configuration), loggerOptions);
    }

    /// <summary>
    /// Registers the Datadog services, without initialising the SDK.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerOptions">
    /// How the <see cref="IDatadogLogger"/> the app resolves is configured.
    /// <see langword="null"/> uses the SDK's own defaults.
    /// </param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <remarks>
    /// Registers <see cref="IRumMonitor"/>, <see cref="IDatadogLogs"/>,
    /// <see cref="IDatadogLogger"/>, <see cref="IDatadogTracer"/> and <see cref="ISessionReplay"/>
    /// as singletons, so a consumer can depend on the piece it uses and be given a substitute in a
    /// test rather than reaching for the static. For the app itself, call
    /// <see cref="AddDatadog(IServiceCollection, DatadogConfiguration, LoggerOptions?)"/> and let
    /// it initialise too; this overload exists for hosts whose SDK is initialised elsewhere, and
    /// for test hosts that want the registrations resolving the no-op neutral implementation.
    /// The <see cref="IDatadogLogger"/> singleton is safe to register this early even though the
    /// SDK is not up yet: a logger created before <see cref="Datadog.Initialize"/> starts
    /// delivering at the first write after it — see <see cref="IDatadogLogger"/>.
    /// </remarks>
    public static IServiceCollection AddDatadog(
        this IServiceCollection services,
        LoggerOptions? loggerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd rather than Add throughout: an app that has already registered its own IRumMonitor
        // - a decorator that adds attributes, or a fake in a test host - keeps it.
        services.TryAddSingleton(_ => Datadog.Rum);
        services.TryAddSingleton(_ => Datadog.Logs);
        services.TryAddSingleton(_ => Datadog.Tracer);
        services.TryAddSingleton(_ => Datadog.SessionReplay);
        services.TryAddSingleton(_ => Datadog.Logs.CreateLogger(loggerOptions));

        // The lifecycle surface too, so "we call SetUser on sign-in" is a unit test against a
        // fake IDatadogSdk rather than an untestable static call.
        services.TryAddSingleton<IDatadogSdk>(DatadogSdk.Instance);

        return services;
    }
}
