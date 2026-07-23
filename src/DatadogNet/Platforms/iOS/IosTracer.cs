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
/// <para>
/// Requires <c>DatadogNet.iOS 2.30.2.2</c> or later. In 2.30.2.1 the OpenTracing protocols were
/// bound as classes rather than interfaces, so <c>DDTracer.Shared</c> — the only way to reach a
/// tracer — threw <see cref="InvalidCastException"/> on first use. See
/// <c>docs/upstream-changes.md</c>.
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

        return new IosSpan(
            DDTracer.Shared.StartSpan(operationName, effectiveParent?.Native.Context, nativeTags));
    }

    public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        if (span is not IosSpan native || Datadog.Configuration?.Trace is not { } options)
        {
            return new Dictionary<string, string>();
        }

        // The writer-as-carrier dance - a different writer type per format, each also being where
        // the result is read back from - lives in DatadogNet.iOS's TracingExtensions rather than
        // here, so a plain .NET iOS app gets it too.
        return DDTracer.Shared.InjectHeaders(native.Native, [.. options.HeaderTypes.Select(ToNativeFormat)]);
    }

    internal static TracingHeaderFormat ToNativeFormat(TracingHeaderType type) => type switch
    {
        TracingHeaderType.B3 => TracingHeaderFormat.B3,
        TracingHeaderType.B3Multi => TracingHeaderFormat.B3Multi,
        TracingHeaderType.TraceContext => TracingHeaderFormat.TraceContext,
        _ => TracingHeaderFormat.Datadog,
    };
}

/// <summary>A span over <c>OTSpan</c>.</summary>
internal sealed class IosSpan(IOTSpan native) : IDatadogSpan
{
    private string? traceId;
    private string? spanId;
    private bool finished;

    internal IOTSpan Native { get; } = native;

    /// <remarks>
    /// Cached because it is not free: dd-sdk-ios's <c>OTSpanContext</c> declares nothing but
    /// <c>forEachBaggageItem</c>, so the ids are derived by injecting into a Datadog-format writer
    /// and parsing the result. dd-sdk-android has them directly, on <c>SpanContext.toTraceId()</c>.
    /// </remarks>
    public string TraceId => traceId ??= Native.GetTraceId();

    /// <inheritdoc cref="TraceId"/>
    public string SpanId => spanId ??= Native.GetSpanId();

    public void SetTag(string key, string value) => Native.SetTag(key, value);

    public void SetTag(string key, double value) => Native.SetTag(key, NSNumber.FromDouble(value));

    public void SetTag(string key, bool value) => Native.SetTag(key, value);

    public void SetError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

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
        _ = TraceId;
        _ = SpanId;

        finished = true;
        ActiveSpanTracker.Finished(this);
        Native.Finish();
    }

    public void Dispose() => Finish();
}
