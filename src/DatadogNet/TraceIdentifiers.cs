using System.Globalization;

namespace DatadogNet;

/// <summary>
/// Renders Datadog trace ids the way Datadog's own SDKs render them.
/// </summary>
/// <remarks>
/// There is one right answer here and it is not a matter of taste: dd-sdk-android's
/// <c>DatadogInterceptor</c> — the reference implementation of RUM-to-APM correlation — writes
/// <c>_dd.trace_id</c> as <c>DatadogTraceId.toHexString()</c> and <c>_dd.span_id</c> as
/// <c>String.valueOf(long)</c>. Trace ids are hexadecimal and span ids are decimal, and the
/// asymmetry is the wire format rather than an oversight.
/// <para>
/// <c>toHexString()</c> is <c>toHexStringPadded(…, 32)</c> on both <c>DD128bTraceId</c> and
/// <c>DD64bTraceId</c>, so the rendering is always 32 lowercase hex characters — a 64-bit id is the
/// low half with sixteen leading zeros, not a 16-character string. That is the shape this produces.
/// </para>
/// <para>
/// Android gets there directly, because <c>DatadogSpanContext</c> hands over a typed
/// <c>DatadogTraceId</c>. iOS has to reassemble it: <c>OTSpanContext</c> exposes no ids at all, and
/// the only route to them is injecting into a Datadog-format headers writer, where the low 64 bits
/// arrive as decimal in <c>x-datadog-trace-id</c> and the high 64 travel separately as
/// <c>_dd.p.tid</c> inside <c>x-datadog-tags</c>. Reading only the former — which is what this
/// façade shipped in 3.14.0.1 — yields a decimal string that names half of a different-looking id.
/// </para>
/// </remarks>
internal static class TraceIdentifiers
{
    /// <summary>The low 64 bits of the trace id, in decimal.</summary>
    internal const string DatadogTraceIdHeader = "x-datadog-trace-id";

    /// <summary>The span id, in decimal.</summary>
    internal const string DatadogSpanIdHeader = "x-datadog-parent-id";

    /// <summary>Comma-separated <c>key=value</c> propagation tags, carrying <c>_dd.p.tid</c>.</summary>
    internal const string DatadogTagsHeader = "x-datadog-tags";

    /// <summary>The propagation tag holding the high 64 bits of a 128-bit trace id.</summary>
    private const string HighOrderBitsTag = "_dd.p.tid";

    /// <summary>The number of hex characters in one 64-bit half.</summary>
    private const int HalfWidth = 16;

    /// <summary>
    /// Rebuilds the full trace id from the two places the Datadog headers split it across.
    /// </summary>
    /// <param name="lowOrderDecimal">The <c>x-datadog-trace-id</c> value: decimal, low 64 bits.</param>
    /// <param name="datadogTags">The <c>x-datadog-tags</c> value, or <see langword="null"/>.</param>
    /// <returns>
    /// 32 lowercase hex characters, or an empty string if <paramref name="lowOrderDecimal"/> is not
    /// a number — there is no trace to name, and inventing one would correlate a RUM resource with
    /// a trace that does not exist.
    /// </returns>
    internal static string ToHexTraceId(string? lowOrderDecimal, string? datadogTags)
    {
        if (!ulong.TryParse(
                lowOrderDecimal?.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var low))
        {
            return string.Empty;
        }

        // Absent _dd.p.tid means 128-bit trace ids are off, which is the DD64bTraceId case: the
        // high half is genuinely zero rather than unknown, and Datadog still pads it out.
        var high = ReadHighOrderBits(datadogTags);

        return high.ToString("x16", CultureInfo.InvariantCulture)
            + low.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads <c>_dd.p.tid</c> out of the propagation tags.</summary>
    private static ulong ReadHighOrderBits(string? datadogTags)
    {
        if (string.IsNullOrEmpty(datadogTags))
        {
            return 0;
        }

        foreach (var pair in datadogTags.Split(','))
        {
            var separator = pair.IndexOf('=');

            if (separator < 0
                || !pair.AsSpan(0, separator).Trim().Equals(HighOrderBitsTag, StringComparison.Ordinal))
            {
                continue;
            }

            var value = pair.AsSpan(separator + 1).Trim();

            // Datadog writes exactly sixteen hex characters. Anything else is a tag written by
            // something this does not understand, and a half-parsed value would name a real-looking
            // trace that nothing ever reported - worse than reporting no high bits at all.
            return value.Length == HalfWidth
                && ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var high)
                    ? high
                    : 0;
        }

        return 0;
    }
}
