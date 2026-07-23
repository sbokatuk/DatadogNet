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

        if (span is not IosSpan native || Datadog.Configuration?.Trace is not { } options)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // InjectHeaders is DatadogNet.iOS's own member. The bound API needs a dance that is not
        // obvious from the signatures - construct a writer, hand it to Inject as though it were the
        // carrier, then read the headers back off the writer - and one writer type per format, so
        // several formats means several round trips. All of that now lives upstream.
        return (IReadOnlyDictionary<string, string>)native.Native.InjectHeaders(
            DDTracer.Shared(), ToNativeFormats(options.HeaderTypes));
    }

    /// <summary>Maps this façade's header types onto the binding's flags.</summary>
    private static OTHeaderFormats ToNativeFormats(IReadOnlyList<TracingHeaderType> types)
    {
        OTHeaderFormats formats = 0;

        foreach (var type in types)
        {
            formats |= type switch
            {
                TracingHeaderType.Datadog => OTHeaderFormats.Datadog,
                TracingHeaderType.TraceContext => OTHeaderFormats.TraceContext,
                TracingHeaderType.B3 => OTHeaderFormats.B3,
                TracingHeaderType.B3Multi => OTHeaderFormats.B3Multi,
                _ => 0,
            };
        }

        return formats;
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

        // SetError(Exception) is DatadogNet.iOS's own overload; SetErrorWithKind takes the three
        // fields separately, and passing only the message leaves the APM error panel empty.
        Native.SetError(exception);
    }

    public void SetError(string kind, string message, string? stack = null) =>
        Native.SetErrorWithKind(kind, message, stack);

    public void Log(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Native.Log(fields);
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

    /// <summary>Reads the ids from the binding's own helpers.</summary>
    /// <remarks>
    /// <c>OTSpanContext</c> declares nothing but <c>forEachBaggageItem</c> — there is no
    /// <c>traceID</c> or <c>spanID</c> to read, on the protocol or on any bound type, and 3.x did
    /// not change that. <c>GetTraceId</c> and <c>GetSpanId</c> in DatadogNet.iOS do the injecting
    /// and reassembling; the trace id arrives in two pieces, the decimal low half in
    /// <c>x-datadog-trace-id</c> and the high half as <c>_dd.p.tid</c> inside <c>x-datadog-tags</c>.
    /// </remarks>
    private void EnsureIds()
    {
        if (traceId is not null)
        {
            return;
        }

        var tracer = DDTracer.Shared();

        traceId = Native.GetTraceId(tracer);
        spanId = Native.GetSpanId(tracer);
    }
}
