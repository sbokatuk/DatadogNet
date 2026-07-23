namespace DatadogNet;

/// <summary>
/// Everything the SDK needs to start, and which of its features to turn on.
/// </summary>
/// <remarks>
/// Passed once to <see cref="Datadog.Initialize"/>. A feature is enabled by assigning its options
/// object and left off by leaving it <see langword="null"/>:
/// <code>
/// Datadog.Initialize (new DatadogConfiguration {
///     ClientToken     = "…",
///     Env             = "production",
///     Service         = "my-app",
///     Site            = DatadogSite.Us1,
///     TrackingConsent = TrackingConsent.Granted,
///     Rum             = new RumOptions { ApplicationId = "…" },
///     Logs            = new LogsOptions (),
/// });
/// </code>
/// <para>
/// One call rather than the native shape of "initialise, then enable each feature in an order that
/// matters". Both native SDKs require the core to be initialised before a feature is enabled, and
/// Session Replay to come after RUM; getting either wrong fails silently, with the feature simply
/// never producing data. Here the ordering is this type's problem rather than yours.
/// </para>
/// </remarks>
public sealed class DatadogConfiguration
{
    /// <summary>
    /// The client token from <c>Organization Settings → Client Tokens</c>.
    /// </summary>
    /// <remarks>
    /// A client token, not an API key. It ships inside your app and is only allowed to submit data,
    /// which is why it is safe to embed and an API key is not.
    /// </remarks>
    public required string ClientToken { get; init; }

    /// <summary>
    /// The environment tag applied to every event — <c>production</c>, <c>staging</c>, and so on.
    /// </summary>
    /// <remarks>
    /// Datadog indexes on this, so it is worth being consistent with what your backend services
    /// report. Must be non-empty; both SDKs reject an empty environment.
    /// </remarks>
    public required string Env { get; init; }

    /// <summary>
    /// The service name events are attributed to. Defaults to the application's bundle or package
    /// identifier.
    /// </summary>
    public string? Service { get; init; }

    /// <summary>
    /// Which Datadog site to upload to. Defaults to <see cref="DatadogSite.Us1"/>.
    /// </summary>
    /// <remarks>
    /// The most common cause of "nothing appears in Datadog". See <see cref="DatadogSite"/>.
    /// </remarks>
    public DatadogSite Site { get; init; } = DatadogSite.Us1;

    /// <summary>
    /// Whether the SDK may collect and upload. Defaults to <see cref="TrackingConsent.Pending"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="TrackingConsent.Pending"/> rather than <see cref="TrackingConsent.Granted"/> is
    /// deliberate, and differs from what a native <c>initialize</c> call makes convenient. Pending
    /// collects and holds without uploading, so an app that has not yet asked its user loses
    /// nothing by starting here and calling <see cref="Datadog.SetTrackingConsent"/> when it has an
    /// answer — whereas defaulting to granted uploads data the user has not agreed to, and no
    /// later call takes it back.
    /// </remarks>
    public TrackingConsent TrackingConsent { get; init; } = TrackingConsent.Pending;

    /// <summary>
    /// How loudly the SDK reports its own problems. Defaults to
    /// <see cref="DatadogVerbosity.None"/>.
    /// </summary>
    public DatadogVerbosity Verbosity { get; init; } = DatadogVerbosity.None;

    /// <summary>How much data is gathered into one upload.</summary>
    public BatchSize BatchSize { get; init; } = BatchSize.Medium;

    /// <summary>How often an upload is attempted.</summary>
    public UploadFrequency UploadFrequency { get; init; } = UploadFrequency.Average;

    /// <summary>How many batches may be processed per upload cycle.</summary>
    public BatchProcessingLevel BatchProcessingLevel { get; init; } = BatchProcessingLevel.Medium;

    /// <summary>
    /// The build variant, reported as a tag. **Android only**; ignored on iOS.
    /// </summary>
    /// <remarks>
    /// Corresponds to a Gradle product flavour. A .NET Android app usually has none, which is why
    /// this defaults to empty rather than being required as the native
    /// <c>Configuration.Builder</c> constructor makes it.
    /// </remarks>
    public string Variant { get; init; } = string.Empty;

    /// <summary>
    /// Hosts whose requests are part of your own backend, and the trace format to propagate to
    /// each.
    /// </summary>
    /// <remarks>
    /// Only requests to these hosts get tracing headers attached, so a trace started in the app
    /// continues into your services. Anything not listed is reported as a RUM resource but is never
    /// given headers — you do not want a Datadog trace id landing on a third-party API.
    /// <code>
    /// FirstPartyHosts = new Dictionary&lt;string, IReadOnlyList&lt;TracingHeaderType&gt;&gt; {
    ///     ["api.example.com"] = [TracingHeaderType.Datadog, TracingHeaderType.TraceContext],
    /// }
    /// </code>
    /// Matching is by host suffix, so <c>example.com</c> also covers <c>api.example.com</c>.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<TracingHeaderType>>? FirstPartyHosts { get; init; }

    /// <summary>
    /// Whether the SDK reports uncaught exceptions. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// **Android only**, and only for JVM-level crashes — which is where an unhandled .NET
    /// exception ends up, so this is the setting that covers a MAUI app's own crashes there.
    /// <para>
    /// iOS has no equivalent switch: crash reporting is an entire separate framework, and needs the
    /// <c>DatadogNet.CrashReporting</c> package. That package also adds native (NDK) crash capture
    /// on Android, which this setting does not cover.
    /// </para>
    /// </remarks>
    public bool CrashReportsEnabled { get; init; } = true;

    /// <summary>
    /// Free-form values passed through to the SDK's own additional-configuration map.
    /// </summary>
    /// <remarks>
    /// Used for settings Datadog documents by string key rather than by API, such as
    /// <c>_dd.first_party_hosts</c> overrides or the internal telemetry toggles.
    /// </remarks>
    public IReadOnlyDictionary<string, object?>? AdditionalConfiguration { get; init; }

    /// <summary>Real User Monitoring. Leave <see langword="null"/> to not enable RUM.</summary>
    public RumOptions? Rum { get; init; }

    /// <summary>Log collection. Leave <see langword="null"/> to not enable Logs.</summary>
    public LogsOptions? Logs { get; init; }

    /// <summary>Distributed tracing. Leave <see langword="null"/> to not enable Trace.</summary>
    public TraceOptions? Trace { get; init; }

    /// <summary>
    /// Session Replay. Leave <see langword="null"/> to not enable it.
    /// </summary>
    /// <remarks>Requires <see cref="Rum"/>; enabling it without RUM records nothing.</remarks>
    public SessionReplayOptions? SessionReplay { get; init; }

    /// <summary>
    /// Reaches the native configuration object just before the SDK is initialised, for anything
    /// this façade does not expose.
    /// </summary>
    /// <remarks>
    /// The argument is <c>Com.Datadog.Android.Core.Configuration.Configuration.Builder</c> on
    /// Android and <c>DatadogObjc.DDConfiguration</c> on iOS. It is never invoked on a platform
    /// with no Datadog support.
    /// <para>
    /// Typed as <see cref="object"/> because the shared API cannot name either type. Cast it under
    /// a platform conditional:
    /// </para>
    /// <code>
    /// ConfigureNative = native => {
    /// #if ANDROID
    ///     ((Com.Datadog.Android.Core.Configuration.Configuration.Builder) native)
    ///         .SetUseDeveloperModeWhenDebuggable (true);
    /// #elif IOS
    ///     ((DatadogObjc.DDConfiguration) native).BackgroundTasksEnabled = true;
    /// #endif
    /// };
    /// </code>
    /// This exists so that the façade covering less than the native SDK is never a dead end. Every
    /// feature options type has the same hook, and each fires at the one moment its setting can
    /// still be applied — several native settings, event mappers in particular, are configuration
    /// time only and cannot be reached after <see cref="Datadog.Initialize"/> returns.
    /// </remarks>
    public Action<object>? ConfigureNative { get; init; }
}
