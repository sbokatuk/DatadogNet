using Com.Datadog.Android;

namespace DatadogNet;

/// <summary>
/// Converts one attribute value, for the native members that take a single value rather than a
/// map.
/// </summary>
/// <remarks>
/// <c>RumMonitor.addAttribute</c>, <c>addFeatureFlagEvaluation</c> and <c>Logger.addAttribute</c>
/// all take a bare <c>java.lang.Object</c>, but <c>DatadogNet.Android</c>'s
/// <c>DatadogAttributes</c> only exposes the map form — its per-value <c>ToJava</c> is private.
/// Rather than reimplement the conversion here and let the two drift, this round-trips through the
/// public map API.
/// <para>
/// The allocation is one dictionary per call, on a path that runs when an app sets a global
/// attribute or records a feature flag — not per event. Making the per-value converter public
/// upstream removes it; see <c>docs/upstream-changes.md</c>.
/// </para>
/// </remarks>
internal static class NativeAttributes
{
    /// <summary>Converts a single value to its Java form.</summary>
    /// <param name="key">The attribute name, used only to name the value in any error.</param>
    /// <param name="value">The value.</param>
    internal static Java.Lang.Object Single(string key, object? value) =>
        DatadogAttributes.From(new Dictionary<string, object?> { [key] = value })[key];
}
