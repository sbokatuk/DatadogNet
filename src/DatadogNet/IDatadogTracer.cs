namespace DatadogNet;

/// <summary>
/// A unit of work in a distributed trace.
/// </summary>
/// <remarks>
/// Started by <see cref="IDatadogTracer.StartSpan"/>. Disposing finishes it, which is the point of
/// the shape: a span that is never finished is never sent, and a <see langword="using"/> block
/// finishes it however the block is left.
/// <code>
/// using var span = Datadog.Tracer.StartSpan ("checkout");
/// span.SetTag ("cart.items", 3);
/// </code>
/// </remarks>
public interface IDatadogSpan : IDisposable
{
    /// <summary>The trace this span belongs to, as a decimal string.</summary>
    /// <remarks>
    /// Empty when tracing is not enabled. Useful for correlating a log line written outside the
    /// SDK, or for logging what to search for in APM.
    /// </remarks>
    string TraceId { get; }

    /// <summary>This span's own id, as a decimal string.</summary>
    string SpanId { get; }

    /// <summary>Tags this span with a string value.</summary>
    void SetTag(string key, string value);

    /// <summary>Tags this span with a numeric value.</summary>
    void SetTag(string key, double value);

    /// <summary>Tags this span with a boolean value.</summary>
    void SetTag(string key, bool value);

    /// <summary>
    /// Marks the span as failed and attaches the exception's type, message and stack.
    /// </summary>
    /// <remarks>
    /// A span marked as an error also surfaces as a RUM error when
    /// <see cref="TraceOptions.BundleWithRumEnabled"/> is on, which is how a failed backend call
    /// shows up on the session it happened in.
    /// </remarks>
    void SetError(Exception exception);

    /// <summary>Marks the span as failed, without an exception.</summary>
    void SetError(string kind, string message, string? stack = null);

    /// <summary>Attaches a structured log to this span.</summary>
    void Log(IReadOnlyDictionary<string, object?> fields);

    /// <summary>
    /// Makes this span the active one for as long as the returned scope lives.
    /// </summary>
    /// <remarks>
    /// Spans started while it is active become its children, and logs written while it is active
    /// are correlated with it. Activation follows the native SDKs' own thread-local scope managers,
    /// so it does <b>not</b> flow across an <see langword="await"/>: activate inside the
    /// continuation, or pass the span as an explicit parent.
    /// </remarks>
    IDisposable Activate();

    /// <summary>Finishes the span. Idempotent; <see cref="IDisposable.Dispose"/> calls it.</summary>
    void Finish();
}

/// <summary>
/// Starts spans and propagates them onto outgoing requests.
/// </summary>
/// <remarks>
/// Reached through <see cref="Datadog.Tracer"/>. Every member is safe to call before
/// <see cref="Datadog.Initialize"/> or on a platform with no Datadog support; you get a span that
/// does nothing rather than an exception.
/// </remarks>
public interface IDatadogTracer
{
    /// <summary>Whether Trace was enabled by <see cref="DatadogConfiguration.Trace"/>.</summary>
    bool IsEnabled { get; }

    /// <summary>The span currently active on this thread, if any.</summary>
    IDatadogSpan? ActiveSpan { get; }

    /// <summary>Starts a span.</summary>
    /// <param name="operationName">
    /// What the span measures — <c>http.request</c>, <c>db.query</c>, <c>checkout</c>. Low
    /// cardinality: it is what APM groups by.
    /// </param>
    /// <param name="parent">
    /// The parent span. Defaults to <see cref="ActiveSpan"/>, which is what you want inside an
    /// <see cref="IDatadogSpan.Activate"/> scope.
    /// </param>
    /// <param name="tags">Tags applied at creation.</param>
    IDatadogSpan StartSpan(
        string operationName,
        IDatadogSpan? parent = null,
        IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>
    /// Writes a span's trace context into a set of HTTP headers, so the trace continues into the
    /// service being called.
    /// </summary>
    /// <param name="span">The span to propagate. Usually the one around the request.</param>
    /// <returns>
    /// The headers to add to the request, in the formats
    /// <see cref="TraceOptions.HeaderTypes"/> selected. Empty when tracing is not enabled.
    /// </returns>
    /// <remarks>
    /// <see cref="DatadogHttpMessageHandler"/> applies this for you on first-party hosts. Call it
    /// directly when you are building a request some other way — a gRPC call, a WebSocket
    /// handshake, a signed URL.
    /// <para>
    /// Only send these to services you control. A Datadog trace id on a third-party request leaks
    /// your internal topology, which is why <see cref="DatadogConfiguration.FirstPartyHosts"/> is
    /// an allowlist rather than a filter.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, string> Inject(IDatadogSpan span);
}
