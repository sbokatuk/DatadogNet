using DatadogCore;
using DatadogTrace;
using Foundation;

namespace DatadogNet;

/// <summary>Tracing over <c>DDTracer</c>, which implements the OpenTracing <c>OTTracer</c>.</summary>
/// <remarks>
/// dd-sdk-ios kept OpenTracing in its Objective-C layer through 3.x, where dd-sdk-android removed it
/// — so this is the one place where the 3.x upgrade moved the *Android* implementation a long way
/// and left this one almost alone. What changed here is smaller than it looks: the header writers no
/// longer take a sampling argument, because sampling is derived from the RUM <c>session.id</c>.
/// <para>
/// The 3.x binding declares the protocols as <c>IOTTracer</c>, <c>IOTSpan</c> and
/// <c>IOTSpanContext</c> — the interface forms. The 2.x binding declared them as classes, which made
/// <c>DDTracer.Shared</c> throw on first use and tracing unreachable from C#; that defect does not
/// exist here.
/// </para>
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

        // Shared() is a method in 3.x, where 2.x had a static property.
        return new IosSpan(
            DDTracer.Shared().StartSpan(operationName, effectiveParent?.Native.Context, nativeTags));
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
    /// <c>TraceContextInjection.All</c> so the headers still go out for a dropped trace, which is
    /// what lets the receiving service stitch the request together even when nothing is stored.
    /// <para>
    /// The 2.x writers additionally took a <c>DDTraceSamplingStrategy</c>, and getting it wrong
    /// propagated "sampled" in one format and "dropped" in another on the same request. 3.x removed
    /// the argument outright — sampling follows the RUM session — so that class of mistake is gone.
    /// </para>
    /// </remarks>
    internal static IEnumerable<KeyValuePair<string, string>> InjectOne(IosSpan span, TracingHeaderType type)
    {
        NSDictionary<NSString, NSString> fields;

        switch (type)
        {
            case TracingHeaderType.Datadog:
                var datadog = new DDHTTPHeadersWriter(DDTraceContextInjection.All);
                DDTracer.Shared().Inject(span.Native.Context, OT.FormatTextMap, datadog, out _);
                fields = datadog.TraceHeaderFields;
                break;

            case TracingHeaderType.TraceContext:
                var w3c = new DDW3CHTTPHeadersWriter(DDTraceContextInjection.All);
                DDTracer.Shared().Inject(span.Native.Context, OT.FormatTextMap, w3c, out _);
                fields = w3c.TraceHeaderFields;
                break;

            case TracingHeaderType.B3:
            case TracingHeaderType.B3Multi:
                var b3 = new DDB3HTTPHeadersWriter(
                    type == TracingHeaderType.B3 ? DDInjectEncoding.Single : DDInjectEncoding.Multiple,
                    DDTraceContextInjection.All);
                DDTracer.Shared().Inject(span.Native.Context, OT.FormatTextMap, b3, out _);
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
internal sealed class IosSpan(IOTSpan native) : IDatadogSpan
{
    private string? traceId;
    private string? spanId;
    private bool finished;

    internal IOTSpan Native { get; } = native;

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
        //
        // A method in 3.x, where the 2.x binding projected setActive as a property.
        Native.SetActive();

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

    /// <summary>Reads the ids back out of a Datadog-format injection.</summary>
    /// <remarks>
    /// dd-sdk-ios's <c>OTSpanContext</c> declares nothing but <c>forEachBaggageItem</c> — there is
    /// no <c>traceID</c> or <c>spanID</c> to read, on the protocol or on any bound type, and 3.x did
    /// not change that. Injecting into a Datadog-format writer and parsing what comes out is the
    /// only route to them from Objective-C.
    /// <para>
    /// The trace id arrives in two pieces, which is why it is reassembled rather than taken from
    /// the header directly — see <see cref="TraceIdentifiers"/>. The span id needs no such work:
    /// <c>x-datadog-parent-id</c> is already the decimal form Datadog correlates on.
    /// </para>
    /// </remarks>
    private void EnsureIds()
    {
        if (traceId is not null)
        {
            return;
        }

        spanId = string.Empty;

        string? lowOrderBits = null;
        string? tags = null;

        foreach (var field in IosTracer.InjectOne(this, TracingHeaderType.Datadog))
        {
            if (field.Key.Equals(TraceIdentifiers.DatadogTraceIdHeader, StringComparison.OrdinalIgnoreCase))
            {
                lowOrderBits = field.Value;
            }
            else if (field.Key.Equals(TraceIdentifiers.DatadogSpanIdHeader, StringComparison.OrdinalIgnoreCase))
            {
                spanId = field.Value;
            }
            else if (field.Key.Equals(TraceIdentifiers.DatadogTagsHeader, StringComparison.OrdinalIgnoreCase))
            {
                tags = field.Value;
            }
        }

        traceId = TraceIdentifiers.ToHexTraceId(lowOrderBits, tags);
    }
}
