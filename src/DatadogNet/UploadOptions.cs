namespace DatadogNet;

/// <summary>
/// How much data the SDK gathers into one upload.
/// </summary>
/// <remarks>
/// Trades battery and network use against how quickly an event reaches Datadog. Larger batches
/// mean fewer, bigger requests.
/// </remarks>
public enum BatchSize
{
    /// <summary>Small batches, uploaded sooner. Costs more requests and more battery.</summary>
    Small,

    /// <summary>The SDK's default.</summary>
    Medium,

    /// <summary>Large batches, uploaded later. Cheapest, and the slowest to show up.</summary>
    Large,
}

/// <summary>How often the SDK attempts an upload.</summary>
public enum UploadFrequency
{
    /// <summary>Most often. Costs the most battery.</summary>
    Frequent,

    /// <summary>The SDK's default.</summary>
    Average,

    /// <summary>Least often. Cheapest, and the slowest to show up.</summary>
    Rare,
}

/// <summary>
/// How many batches the SDK is allowed to process in a single upload cycle.
/// </summary>
/// <remarks>
/// Raise it when an app produces events faster than they drain — a long session with Session
/// Replay on, typically — at the cost of more work per cycle.
/// </remarks>
public enum BatchProcessingLevel
{
    /// <summary>One batch per cycle.</summary>
    Low,

    /// <summary>The SDK's default.</summary>
    Medium,

    /// <summary>The most batches per cycle.</summary>
    High,
}

/// <summary>
/// How loudly the SDK reports its own problems to the platform log.
/// </summary>
/// <remarks>
/// This is the SDK talking about itself — an invalid client token, a feature enabled before
/// initialisation, a dropped event — not your app's logs. Worth turning up to
/// <see cref="Warn"/> or <see cref="Debug"/> while integrating; it is where "your client token is
/// invalid" appears. Output goes to logcat on Android and the Xcode console on iOS.
/// </remarks>
public enum DatadogVerbosity
{
    /// <summary>Say nothing. The default.</summary>
    None,

    /// <summary>Everything, including per-batch upload detail.</summary>
    Debug,

    /// <summary>Problems that did not stop the SDK working.</summary>
    Warn,

    /// <summary>Problems that did.</summary>
    Error,

    /// <summary>Only the unrecoverable.</summary>
    Critical,
}

/// <summary>
/// The wire format used to propagate a trace onto an outgoing request.
/// </summary>
/// <remarks>
/// Has to match what the receiving service understands. <see cref="Datadog"/> is the native
/// format; <see cref="TraceContext"/> is the W3C standard and the safest choice for a backend you
/// do not control.
/// </remarks>
public enum TracingHeaderType
{
    /// <summary>Datadog's own <c>x-datadog-*</c> headers.</summary>
    Datadog,

    /// <summary>B3 single-header format (<c>b3</c>).</summary>
    B3,

    /// <summary>B3 multi-header format (<c>X-B3-TraceId</c> and friends).</summary>
    B3Multi,

    /// <summary>W3C Trace Context (<c>traceparent</c>).</summary>
    TraceContext,
}
