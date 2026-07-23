using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DatadogNet.Maui;

/// <summary>
/// Wires Datadog into a MAUI app in one call.
/// </summary>
public static partial class MauiAppBuilderExtensions
{
    /// <summary>
    /// Initialises Datadog and wires it into the app.
    /// </summary>
    /// <param name="builder">The MAUI app builder.</param>
    /// <param name="configuration">What to start, and how.</param>
    /// <param name="options">
    /// What the MAUI integration should wire up. <see langword="null"/> takes the defaults, which
    /// turn everything on.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// <code>
    /// public static MauiApp CreateMauiApp ()
    /// {
    ///     var builder = MauiApp.CreateBuilder ();
    ///
    ///     builder
    ///         .UseMauiApp&lt;App&gt; ()
    ///         .UseDatadog (new DatadogConfiguration {
    ///             ClientToken     = "…",
    ///             Env             = "production",
    ///             Service         = "my-app",
    ///             TrackingConsent = TrackingConsent.Granted,
    ///             Rum             = new RumOptions { ApplicationId = "…" },
    ///             Logs            = new LogsOptions (),
    ///             Trace           = new TraceOptions (),
    ///             SessionReplay   = new SessionReplayOptions (),
    ///         });
    ///
    ///     return builder.Build ();
    /// }
    /// </code>
    /// The SDK is initialised <b>during this call</b> rather than when the app starts, which is as
    /// early as a MAUI app can manage from shared code — crash reporting only covers what happens
    /// after the SDK is up, and startup failures are the ones worth catching. Put it directly after
    /// <c>UseMauiApp</c>, before any registration that could itself throw.
    /// <para>
    /// Registers <see cref="IRumMonitor"/>, <see cref="IDatadogLogs"/>, <see cref="IDatadogLogger"/>,
    /// <see cref="IDatadogTracer"/> and <see cref="ISessionReplay"/> as singletons, so a view-model
    /// can depend on the piece it uses and be given a substitute in a test rather than reaching for
    /// the static.
    /// </para>
    /// <para>
    /// Crash reporting is not turned on here: it lives in a separate package because it installs a
    /// signal handler. Add <c>DatadogNet.CrashReporting</c> and call <c>CrashReporting.Enable()</c>
    /// on the next line.
    /// </para>
    /// </remarks>
    public static MauiAppBuilder UseDatadog(
        this MauiAppBuilder builder,
        DatadogConfiguration configuration,
        DatadogMauiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        options ??= new DatadogMauiOptions();

        Datadog.Initialize(configuration);
        DatadogTracking.Configure(options);

        // TryAdd rather than Add throughout: an app that has already registered its own IRumMonitor
        // - a decorator that adds attributes, or a fake in a test host - keeps it.
        builder.Services.TryAddSingleton(_ => Datadog.Rum);
        builder.Services.TryAddSingleton(_ => Datadog.Logs);
        builder.Services.TryAddSingleton(_ => Datadog.Tracer);
        builder.Services.TryAddSingleton(_ => Datadog.SessionReplay);
        builder.Services.TryAddSingleton(_ => Datadog.Logs.CreateLogger(options.Logger));

        if (options.AddLoggingProvider)
        {
            builder.Logging.AddDatadog(options);
        }

        if (options.TrackUnhandledExceptions)
        {
            UnhandledExceptionReporting.Enable();
        }

        if (options.TrackPageViews)
        {
            // Deferred to a platform lifecycle hook: Application.Current is null at builder time and
            // stays null until the platform creates the app object.
            AttachNavigationTracking(builder);
        }

        return builder;
    }

    /// <summary>
    /// Sends <c>Microsoft.Extensions.Logging</c> output to Datadog.
    /// </summary>
    /// <remarks>
    /// Called for you by <see cref="UseDatadog"/> unless
    /// <see cref="DatadogMauiOptions.AddLoggingProvider"/> is off. Public so an app that configures
    /// logging separately can add it alongside its other providers.
    /// </remarks>
    public static ILoggingBuilder AddDatadog(this ILoggingBuilder builder, DatadogMauiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var effective = options ?? new DatadogMauiOptions();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(_ => new DatadogLoggerProvider(effective)));

        return builder;
    }

    /// <summary>
    /// Subscribes page tracking to the application object, once the platform has created one.
    /// </summary>
    private static partial void AttachNavigationTracking(MauiAppBuilder builder);
}
