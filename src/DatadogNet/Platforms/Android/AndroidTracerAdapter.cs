using Com.Datadog.Android;
using IO.Opentracing;
using IO.Opentracing.Propagation;
using IO.Opentracing.Util;

namespace DatadogNet;

/// <summary>
/// Tracing over <c>AndroidTracer</c>, which implements <c>io.opentracing.Tracer</c>.
/// </summary>
/// <remarks>
/// The tracer is reached through <c>GlobalTracer</c> rather than kept in a field: that is where
/// <c>EnableTrace</c> registers it, and where any other Datadog integration in the process expects
/// to find it.
/// </remarks>
internal sealed class AndroidTracerAdapter : IDatadogTracer
{
    public bool IsEnabled => Datadog.Configuration?.Trace is not null && GlobalTracer.IsRegistered;

    public IDatadogSpan? ActiveSpan => ActiveSpanTracker.Current;

    public IDatadogSpan StartSpan(
        string operationName,
        IDatadogSpan? parent = null,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(operationName);

        var builder = GlobalTracer.Get()!.BuildSpan(operationName)!;

        if ((parent ?? ActiveSpanTracker.Current) is AndroidSpan effectiveParent)
        {
            builder.AsChildOf(effectiveParent.Native.Context());
        }
        else
        {
            // No parent and nothing active: OpenTracing would otherwise adopt whatever the SDK's
            // own scope manager considers active, which for a MAUI app is usually a span some
            // unrelated integration left open.
            builder.IgnoreActiveSpan();
        }

        if (tags is { Count: > 0 })
        {
            foreach (var tag in tags)
            {
                // io.opentracing.Tracer.SpanBuilder overloads withTag on String, Number and
                // boolean; anything else has to become a string, which is also what the Datadog
                // backend stores it as.
                switch (tag.Value)
                {
                    case null:
                        break;
                    case bool flag:
                        builder.WithTag(tag.Key, flag);
                        break;
                    case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                        builder.WithTag(
                            tag.Key,
                            Java.Lang.Double.ValueOf(
                                Convert.ToDouble(tag.Value, System.Globalization.CultureInfo.InvariantCulture)));
                        break;
                    default:
                        builder.WithTag(tag.Key, tag.Value.ToString());
                        break;
                }
            }
        }

        return new AndroidSpan(builder.Start()!);
    }

    public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (span is not AndroidSpan native || !GlobalTracer.IsRegistered)
        {
            return headers;
        }

        // One call writes every header type the tracer was configured with - the formats are a
        // property of AndroidTracer rather than of the writer, which is the reverse of iOS, where
        // each format needs its own writer.
        //
        // A HeaderCollector rather than the SDK's own TextMapInjectAdapter: that adapter's
        // constructor takes an IDictionary<string, string>, which the binding marshals by *copying*
        // into a fresh java.util.HashMap. The Java side then writes the headers into the copy, the
        // managed dictionary never sees them, and injection silently produces nothing. Implementing
        // ITextMapInject makes the SDK call back into managed code instead.
        var carrier = new HeaderCollector(headers);

        GlobalTracer.Get()!.Inject(
            native.Native.Context(),
            IFormat.Builtin.TextMapInject!,
            carrier);

        return headers;
    }

    /// <summary>Collects injected headers straight into a managed dictionary.</summary>
    private sealed class HeaderCollector(IDictionary<string, string> headers)
        : Java.Lang.Object, ITextMapInject
    {
        public void Put(string key, string value) => headers[key] = value;
    }
}

/// <summary>A span over <c>io.opentracing.Span</c>.</summary>
internal sealed class AndroidSpan(ISpan native) : IDatadogSpan
{
    private bool finished;

    internal ISpan Native { get; } = native;

    public string TraceId => Native.Context()?.ToTraceId() ?? string.Empty;

    public string SpanId => Native.Context()?.ToSpanId() ?? string.Empty;

    public void SetTag(string key, string value) => Native.SetTag(key, value);

    // The Number overload, not the generic setTag(Tag<T>, T) one: a bare C# double binds to the
    // generic form and does not compile, which is the same trap DatadogNet.Android's README
    // documents for setTag on a span.
    public void SetTag(string key, double value) => Native.SetTag(key, Java.Lang.Double.ValueOf(value));

    public void SetTag(string key, bool value) => Native.SetTag(key, value);

    public void SetError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        SetError(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.ToString());
    }

    public void SetError(string kind, string message, string? stack = null)
    {
        // io.opentracing has no setError; the Datadog convention is these four log fields, which
        // dd-trace turns into the span's error facets. dd-sdk-ios has a real setErrorWithKind, so
        // this is where the two SDKs' tracing APIs differ most.
        Native.SetTag("error", true);

        var fields = new Dictionary<string, object?>
        {
            ["event"] = "error",
            ["error.kind"] = kind,
            ["message"] = message,
        };

        if (stack is not null)
        {
            fields["stack"] = stack;
        }

        Log(fields);
    }

    public void Log(IReadOnlyDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        // io.opentracing.Span.log takes Map<String, ?>, which the binding projects as
        // IDictionary<string, object> rather than the IDictionary<string, Java.Lang.Object> every
        // Datadog API takes - so DatadogAttributes' output is re-boxed one level up rather than
        // passed through. The values are already Java objects; only the dictionary type differs.
        var converted = DatadogAttributes.From(fields);
        var carrier = new Dictionary<string, object>(converted.Count);

        foreach (var pair in converted)
        {
            carrier[pair.Key] = pair.Value;
        }

        Native.Log(carrier);
    }

    public IDisposable Activate()
    {
        var scope = GlobalTracer.Get()!.ActivateSpan(Native);

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

    /// <summary>Closes an OpenTracing scope on dispose.</summary>
    /// <remarks>
    /// <c>io.opentracing.Scope</c> extends <c>Closeable</c>, so the binding gives it a
    /// <c>Close()</c> rather than the <see cref="IDisposable"/> the façade hands back.
    /// </remarks>
    private sealed class ScopeHandle(IScope? scope) : IDisposable
    {
        public void Dispose() => scope?.Close();
    }
}
