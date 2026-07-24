namespace DatadogNet.Maui;

/// <summary>
/// What the MAUI integration wires up on top of the SDK itself.
/// </summary>
/// <remarks>
/// Everything here is on by default. The defaults are the reason to use this package rather than
/// calling <see cref="Datadog.Initialize"/> yourself, so an options object whose defaults were all
/// <see langword="false"/> would be pointless.
/// </remarks>
public sealed class DatadogMauiOptions
{
    /// <summary>
    /// Report a RUM view for every page the app shows. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// This is the single most valuable thing the MAUI package does, because neither native SDK can
    /// do it: a MAUI page is not a <c>UIViewController</c> and not an <c>Activity</c>. Android
    /// draws the whole app into one Activity, so <c>ActivityViewTrackingStrategy</c> reports one
    /// view for the entire session; iOS reports per view controller, which for a Shell app is
    /// roughly per page but not reliably so, and never matches the route names your code uses.
    /// <para>
    /// Views are named after the page type, or after
    /// <see cref="DatadogTracking.ViewNameProperty"/> where a page sets it.
    /// </para>
    /// </remarks>
    public bool TrackPageViews { get; init; } = true;

    /// <summary>
    /// Report unhandled exceptions as RUM errors before the process dies. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Complements crash reporting rather than replacing it. A managed exception that reaches
    /// <see cref="AppDomain.UnhandledException"/> still has its .NET type, message and stack; by the
    /// time the same failure arrives at a native crash handler it is a signal or a Java exception
    /// with the managed frames flattened away. Reporting it here is what makes it searchable by
    /// exception type.
    /// <para>
    /// Also picks up <see cref="TaskScheduler.UnobservedTaskException"/>, which does not terminate
    /// the process and so is otherwise invisible, and each platform's own boundary — Android's
    /// <c>UnhandledExceptionRaiser</c> and Apple's managed-exception marshalling — where a failure
    /// can end the app without ever reaching the <c>AppDomain</c> hook. A failure seen by more than
    /// one hook is reported once.
    /// </para>
    /// </remarks>
    public bool TrackUnhandledExceptions { get; init; } = true;

    /// <summary>
    /// Send <c>Microsoft.Extensions.Logging</c> output to Datadog. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Registers an <c>ILoggerProvider</c>, so an app that already logs through
    /// <c>ILogger&lt;T&gt;</c> gets its logs into Datadog with no call-site changes, each tagged
    /// with its category and correlated with the RUM view it was written in.
    /// </remarks>
    public bool AddLoggingProvider { get; init; } = true;

    /// <summary>
    /// The lowest level forwarded to Datadog by the logging provider. Defaults to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Information"/>.
    /// </summary>
    /// <remarks>
    /// Separate from the logging framework's own filters, and applied on top of them. Debug and
    /// trace logs are numerous, and each one is an event Datadog bills for.
    /// </remarks>
    public Microsoft.Extensions.Logging.LogLevel MinimumLogLevel { get; init; } =
        Microsoft.Extensions.Logging.LogLevel.Information;

    /// <summary>
    /// How the logger the app resolves from the container is configured.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> uses the SDK's own defaults. This is the logger registered as
    /// <see cref="IDatadogLogger"/> and the one behind the logging provider.
    /// </remarks>
    public LoggerOptions? Logger { get; init; }

    /// <summary>
    /// Names the RUM view for a page. Defaults to the page's type name.
    /// </summary>
    /// <remarks>
    /// Runs for every page as it appears, so it should be cheap and low cardinality — a route, not
    /// a title containing an order number. A page can override it individually with
    /// <see cref="DatadogTracking.ViewNameProperty"/>, which takes precedence over this.
    /// </remarks>
    public Func<Page, string>? ViewName { get; init; }
}
