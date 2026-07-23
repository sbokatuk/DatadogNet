using Com.Datadog.Android;
using Com.Datadog.Android.Trace;

// Propagation carries DatadogNet.Android's hand-written Inject(context, dictionary); Tracer carries
// the generator's own IDatadogTracerExtensions, whose BuildSpan(string) saves wrapping the operation
// name in a Java.Lang.String for the CharSequence the interface declares.
using Com.Datadog.Android.Trace.Api.Propagation;
using Com.Datadog.Android.Trace.Api.Span;
using Com.Datadog.Android.Trace.Api.Tracer;

// Both native interfaces collide with this façade's own names, which is a consequence of Datadog
// having converged on the same vocabulary in 3.x. Aliased once here.
using NativeScope = Com.Datadog.Android.Trace.Api.Scope.IDatadogScope;
using NativeSpan = Com.Datadog.Android.Trace.Api.Span.IDatadogSpan;
using NativeSpanContext = Com.Datadog.Android.Trace.Api.Span.IDatadogSpanContext;

namespace DatadogNet;

/// <summary>
/// Tracing over <c>DatadogTracing</c> and <c>GlobalDatadogTracer</c>.
/// </summary>
/// <remarks>
/// This is the part of the façade that the 3.x upgrade rewrote. dd-sdk-android 3.0 removed the
/// OpenTracing dependency and <c>AndroidTracer</c> with it: spans are <c>DatadogSpan</c>, built
/// through <c>DatadogTracing.NewTracerBuilder(core)</c> and registered on
/// <c>GlobalDatadogTracer</c>.
/// <para>
/// The trade is favourable. <c>DatadogSpan</c> has real <c>SetError</c>, <c>SetErrorMessage</c> and
/// <c>LogErrorMessage</c> members where <c>io.opentracing.Span</c> had none and this file had to
/// spell out Datadog's four-log-field convention by hand and hope the names were right;
/// <c>SetTag</c> has typed overloads where a numeric tag used to bind to the generic
/// <c>setTag(Tag&lt;T&gt;, T)</c> form and fail to compile; and the ids are readable directly.
/// </para>
/// </remarks>
internal sealed class AndroidTracerAdapter : IDatadogTracer
{
    public bool IsEnabled =>
        Datadog.Configuration?.Trace is not null && GlobalDatadogTracer.Instance?.OrNull is not null;

    public IDatadogSpan? ActiveSpan => ActiveSpanTracker.Current;

    public IDatadogSpan StartSpan(
        string operationName,
        IDatadogSpan? parent = null,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(operationName);

        // BuildSpan(string) comes from the generator's IDatadogTracerExtensions. The interface
        // itself declares CharSequence, so calling it directly needs a Java.Lang.String.
        var builder = GlobalDatadogTracer.Get()!.BuildSpan(operationName)!;

        if ((parent ?? ActiveSpanTracker.Current) is AndroidSpan effectiveParent)
        {
            builder.WithParentSpan(effectiveParent.Native);
        }
        else
        {
            // No parent and nothing active: the tracer would otherwise adopt whatever its own scope
            // manager considers active, which for a MAUI app is usually a span some unrelated
            // integration left open.
            builder.IgnoreActiveSpan();
        }

        if (tags is { Count: > 0 })
        {
            // withTag(String, Object) takes anything the SDK can serialise, so the attribute
            // converter's output goes straight through - unlike 2.x, where the OpenTracing builder
            // overloaded on String, Number and boolean and each value had to be dispatched by type.
            var converted = DatadogAttributes.From(tags);

            foreach (var tag in converted)
            {
                builder.WithTag(tag.Key, tag.Value);
            }
        }

        return new AndroidSpan(builder.Start()!);
    }

    public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (span is not AndroidSpan native
            || GlobalDatadogTracer.Instance?.OrNull is null
            || native.Native.Context() is not { } context)
        {
            return headers;
        }

        // One call writes every header type the tracer was configured with - the formats are a
        // property of the tracer rather than of the writer, which is the reverse of iOS.
        //
        // Inject(context, dictionary) is DatadogNet.Android's own overload. The native setter is a
        // Kotlin (C, String, String) -> Unit, which binds as IFunction3 and which C# cannot express
        // as a lambda, so this used to need a Java.Lang.Object subclass here. 2.x had a different
        // trap in the same place - TextMapInjectAdapter's carrier was marshalled by copy, so the
        // SDK wrote the headers into a copy the caller never saw - and both ended the same way,
        // with a request going out untraced and nothing reported.
        GlobalDatadogTracer.Get()!.Propagate()!.Inject(context, headers);

        return headers;
    }
}

/// <summary>A span over <c>DatadogSpan</c>.</summary>
internal sealed class AndroidSpan(NativeSpan native) : IDatadogSpan
{
    private bool finished;

    internal NativeSpan Native { get; } = native;

    /// <remarks>
    /// <c>GetTraceId</c> and <c>GetSpanId</c> are DatadogNet.Android's own members, and they render
    /// the ids the way dd-sdk-android's <c>DatadogInterceptor</c> does when it writes
    /// <c>_dd.trace_id</c> and <c>_dd.span_id</c> onto a RUM resource: the trace id as 32 lowercase
    /// hex characters, the span id as decimal. Matching that exactly is what makes the correlation
    /// work, which is why neither is rendered here.
    /// <para>
    /// iOS reaches the same two strings through <c>OTSpanExtensions</c> in DatadogNet.iOS, which has
    /// to reassemble them from injected headers because <c>OTSpanContext</c> exposes no ids at all.
    /// </para>
    /// </remarks>
    public string TraceId => Native.GetTraceId();

    public string SpanId => Native.GetSpanId();

    public void SetTag(string key, string value) => Native.SetTag(key, value);

    public void SetTag(string key, double value) => Native.SetTag(key, Java.Lang.Double.ValueOf(value));

    public void SetTag(string key, bool value) => Native.SetTag(key, value);

    // SetError is DatadogNet.Android's own overload. In 2.x io.opentracing.Span had no setError at
    // all, so this was an "error" tag plus four log fields written by convention here - and getting
    // one field name wrong produced a span that looked fine and was never counted as an error.
    public void SetError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Native.SetError(exception);
    }

    public void SetError(string kind, string message, string? stack = null) =>
        Native.SetError(kind, message, stack);

    public void Log(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        // LogAttributes takes IDictionary<string, Java.Lang.Object> - the same shape every other
        // Datadog member takes, so unlike 2.x's io.opentracing log(Map<String, ?>) the converter's
        // output needs no re-boxing.
        Native.LogAttributes(DatadogAttributes.From(fields));
    }

    public IDisposable Activate()
    {
        var scope = GlobalDatadogTracer.Get()!.ActivateSpan(Native);

        return ActiveSpanTracker.Activate(this, new ScopeHandle(scope));
    }

    public void Finish()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        ActiveSpanTracker.Finished(this);
        Native.Finish();
    }

    public void Dispose() => Finish();

    /// <summary>Closes a tracer scope on dispose.</summary>
    /// <remarks>
    /// <c>DatadogScope</c> exposes <c>Close()</c> rather than the <see cref="IDisposable"/> the
    /// façade hands back, exactly as <c>io.opentracing.Scope</c> did.
    /// </remarks>
    private sealed class ScopeHandle(NativeScope? scope) : IDisposable
    {
        public void Dispose() => scope?.Close();
    }
}
