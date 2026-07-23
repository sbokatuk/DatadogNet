using DatadogObjc;
using Foundation;

namespace DatadogNet;

/// <summary>Tracing over <c>DDTracer</c>, which implements the OpenTracing <c>OTTracer</c>.</summary>
/// <remarks>
/// dd-sdk-ios 2.x exposes tracing through OpenTracing, exactly as dd-sdk-android 2.x does with
/// <c>AndroidTracer</c> and <c>io.opentracing.Tracer</c>. That symmetry is why
/// <see cref="IDatadogSpan"/> can be one interface rather than two shapes glued together, and it is
/// specific to the 2.x line — 3.0 removed OpenTracing on both platforms in favour of an
/// OpenTelemetry-shaped API.
/// </remarks>
internal sealed class IosTracer : IDatadogTracer
{
    public bool IsEnabled => Datadog.Configuration?.Trace is not null;

    public IDatadogSpan? ActiveSpan => ActiveSpanTracker.Current;

    public IDatadogSpan StartSpan(
        string operationName,
        IDatadogSpan? parent = null,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(operationName);

        var effectiveParent = (parent ?? ActiveSpanTracker.Current) as IosSpan;
        var nativeTags = tags is { Count: > 0 } ? DatadogAttributes.From(tags) : null;

        return new IosSpan(
            DDTracer.Shared.StartSpan(operationName, effectiveParent?.Native.Context, nativeTags));
    }

    public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (span is not IosSpan native || Datadog.Configuration?.Trace is not { } options)
        {
            return headers;
        }

        // One writer per format. dd-sdk-ios has no writer that emits several formats at once -
        // unlike Android, where the header types are a property of the tracer and a single inject
        // call writes all of them - so the formats are looped here and the results merged.
        foreach (var type in options.HeaderTypes)
        {
            foreach (var field in InjectOne(native, type))
            {
                headers[field.Key] = field.Value;
            }
        }

        return headers;
    }

    /// <summary>Injects one header format and reads back what the writer produced.</summary>
    /// <remarks>
    /// <c>HeadBased</c> sampling because the keep-or-drop decision was made when the trace started;
    /// deciding it again per writer would propagate "sampled" in one format and "dropped" in
    /// another on the same request. <c>TraceContextInjection.All</c> so the headers still go out for
    /// a dropped trace, which is what lets the receiving service stitch the request together even
    /// when nothing is stored.
    /// </remarks>
    internal static IEnumerable<KeyValuePair<string, string>> InjectOne(IosSpan span, TracingHeaderType type)
    {
        NSObject writer;
        NSDictionary<NSString, NSString> fields;

        switch (type)
        {
            case TracingHeaderType.Datadog:
                var datadog = new DDHTTPHeadersWriter(DDTraceSamplingStrategy.HeadBased, DDTraceContextInjection.All);
                writer = datadog;
                DDTracer.Shared.Inject(span.Native.Context, OT.FormatTextMap, writer, out _);
                fields = datadog.TraceHeaderFields;
                break;

            case TracingHeaderType.TraceContext:
                var w3c = new DDW3CHTTPHeadersWriter(DDTraceSamplingStrategy.HeadBased, DDTraceContextInjection.All);
                writer = w3c;
                DDTracer.Shared.Inject(span.Native.Context, OT.FormatTextMap, writer, out _);
                fields = w3c.TraceHeaderFields;
                break;

            case TracingHeaderType.B3:
            case TracingHeaderType.B3Multi:
                var b3 = new DDB3HTTPHeadersWriter(
                    DDTraceSamplingStrategy.HeadBased,
                    type == TracingHeaderType.B3 ? DDInjectEncoding.Single : DDInjectEncoding.Multiple,
                    DDTraceContextInjection.All);
                writer = b3;
                DDTracer.Shared.Inject(span.Native.Context, OT.FormatTextMap, writer, out _);
                fields = b3.TraceHeaderFields;
                break;

            default:
                yield break;
        }

        foreach (var key in fields.Keys)
        {
            yield return new KeyValuePair<string, string>(key.ToString(), fields[key].ToString());
        }
    }
}

/// <summary>A span over <c>OTSpan</c>.</summary>
internal sealed class IosSpan(OTSpan native) : IDatadogSpan
{
    /// <summary>
    /// The Datadog-format header names the trace and span ids are read out of.
    /// </summary>
    /// <remarks>
    /// dd-sdk-ios's <c>OTSpanContext</c> declares nothing but <c>forEachBaggageItem</c> — there is
    /// no <c>traceID</c> or <c>spanID</c> to read, on the protocol or on any bound type. Injecting
    /// into a Datadog-format writer and parsing what comes out is the only route to them from
    /// Objective-C, and these two headers are where they land.
    /// <para>
    /// dd-sdk-android has the ids directly, on <c>SpanContext.toTraceId()</c> and
    /// <c>toSpanId()</c>, so this asymmetry is the iOS SDK's rather than the binding's. It is what
    /// <c>DatadogHttpMessageHandler</c> needs to write <c>_dd.trace_id</c> onto a RUM resource, and
    /// so is what links a RUM resource to its APM trace.
    /// </para>
    /// </remarks>
    private const string TraceIdHeader = "x-datadog-trace-id";

    private const string SpanIdHeader = "x-datadog-parent-id";

    private string? traceId;
    private string? spanId;
    private bool finished;

    internal OTSpan Native { get; } = native;

    public string TraceId
    {
        get
        {
            EnsureIds();
            return traceId ?? string.Empty;
        }
    }

    public string SpanId
    {
        get
        {
            EnsureIds();
            return spanId ?? string.Empty;
        }
    }

    public void SetTag(string key, string value) => Native.SetTag(key, value);

    public void SetTag(string key, double value) => Native.SetTag(key, NSNumber.FromDouble(value));

    public void SetTag(string key, bool value) => Native.SetTag(key, value);

    public void SetError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Native.SetErrorWithKind(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace);
    }

    public void SetError(string kind, string message, string? stack = null) =>
        Native.SetErrorWithKind(kind, message, stack);

    public void Log(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Native.Log(DatadogAttributes.From(fields));
    }

    public IDisposable Activate()
    {
        // OTSpan.setActive pushes onto the SDK's own active-span stack, and finishing the span pops
        // it again; there is no explicit deactivate to call. So the returned scope restores this
        // façade's view of what is active and leaves the SDK's to the span's lifetime - which is
        // why finishing a span inside its own using block is the shape to write.
        _ = Native.SetActive;

        return ActiveSpanTracker.Activate(this, nativeScope: null);
    }

    public void Finish()
    {
        if (finished)
        {
            return;
        }

        // Read before finishing: injecting into a finished span's context is not something the SDK
        // promises to answer, and the ids are what a caller most often wants after the fact.
        EnsureIds();

        finished = true;
        ActiveSpanTracker.Finished(this);
        Native.Finish();
    }

    public void Dispose() => Finish();

    private void EnsureIds()
    {
        if (traceId is not null)
        {
            return;
        }

        traceId = string.Empty;
        spanId = string.Empty;

        foreach (var field in IosTracer.InjectOne(this, TracingHeaderType.Datadog))
        {
            if (field.Key.Equals(TraceIdHeader, StringComparison.OrdinalIgnoreCase))
            {
                traceId = field.Value;
            }
            else if (field.Key.Equals(SpanIdHeader, StringComparison.OrdinalIgnoreCase))
            {
                spanId = field.Value;
            }
        }
    }
}
