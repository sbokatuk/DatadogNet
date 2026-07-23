namespace DatadogNet;

/// <summary>
/// Distributed tracing (APM).
/// </summary>
/// <remarks>
/// Assign to <see cref="DatadogConfiguration.Trace"/> to enable Trace. Spans are then started
/// through <see cref="Datadog.Tracer"/>.
/// <para>
/// Both 2.x SDKs are OpenTracing-shaped — <c>AndroidTracer</c> implements
/// <c>io.opentracing.Tracer</c> and <c>DDTracer</c> implements <c>OTTracer</c> — which is why
/// <see cref="IDatadogTracer"/> can be one interface over both. dd-sdk 3.0 removed OpenTracing on
/// both platforms, so this shape is specific to the 2.x line.
/// </para>
/// </remarks>
public sealed class TraceOptions
{
    /// <summary>Percentage of traces kept, 0 to 100. Defaults to 100.</summary>
    public float SampleRate { get; init; } = 100;

    /// <summary>The service spans are attributed to. Defaults to the SDK's service.</summary>
    public string? Service { get; init; }

    /// <summary>Attach network connectivity information to each span.</summary>
    public bool NetworkInfoEnabled { get; init; }

    /// <summary>
    /// Correlate spans with the current RUM view. Defaults to <see langword="true"/>.
    /// </summary>
    public bool BundleWithRumEnabled { get; init; } = true;

    /// <summary>Tags applied to every span this tracer creates.</summary>
    public IReadOnlyDictionary<string, string>? GlobalTags { get; init; }

    /// <summary>
    /// Which header formats <see cref="IDatadogTracer.Inject"/> writes.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="TracingHeaderType.Datadog"/> and
    /// <see cref="TracingHeaderType.TraceContext"/> together, which is what reaches both a Datadog
    /// -instrumented backend and a W3C-compliant one without having to know which you have.
    /// </remarks>
    public IReadOnlyList<TracingHeaderType> HeaderTypes { get; init; } =
        [TracingHeaderType.Datadog, TracingHeaderType.TraceContext];

    /// <summary>Send spans somewhere other than the site's intake. For a proxy or a local test.</summary>
    public Uri? CustomEndpoint { get; init; }

    /// <summary>
    /// Reaches the native Trace configuration before Trace is enabled.
    /// </summary>
    /// <remarks>
    /// The argument is <c>Com.Datadog.Android.Trace.TraceConfiguration.Builder</c> on Android and
    /// <c>DatadogObjc.DDTraceConfiguration</c> on iOS. This is where the span event mapper lives,
    /// and on iOS the <c>DDTraceURLSessionTracking</c> first-party-host helper.
    /// </remarks>
    public Action<object>? ConfigureNative { get; init; }
}
