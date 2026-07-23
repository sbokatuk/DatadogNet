using DatadogObjc;
using Foundation;

namespace DatadogNet;

/// <summary>
/// Converts one attribute value, for the native members that take a single value rather than a
/// dictionary.
/// </summary>
/// <remarks>
/// <c>DDRUMMonitor.AddAttributeForKey</c> and <c>AddFeatureFlagEvaluationWithName</c> take a bare
/// <see cref="NSObject"/>, but <c>DatadogNet.iOS</c>'s <c>DatadogAttributes</c> only exposes the
/// dictionary form — its per-value <c>ToNSObject</c> is private. Rather than reimplement the
/// conversion here and let the two drift, this round-trips through the public dictionary API.
/// <para>
/// The allocation is one <see cref="NSDictionary"/> per call, on a path that runs when an app sets
/// a global attribute or records a feature flag — not per event. Making the per-value converter
/// public upstream removes it; see <c>docs/upstream-changes.md</c>.
/// </para>
/// </remarks>
internal static class NativeAttributes
{
    /// <summary>Converts a single value to its Objective-C form.</summary>
    /// <param name="key">The attribute name, used only to name the value in any error.</param>
    /// <param name="value">The value.</param>
    internal static NSObject Single(string key, object? value)
    {
        var converted = DatadogAttributes.From(new Dictionary<string, object?> { [key] = value });

        // Never null: From always produces an entry per key, using NSNull for a null value.
        return converted.ObjectForKey(new NSString(key))!;
    }
}
