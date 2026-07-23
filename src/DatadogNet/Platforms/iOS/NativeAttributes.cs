using DatadogCore;
using Foundation;

namespace DatadogNet;

/// <summary>
/// Converts one attribute value, for the native members that take a single value rather than a
/// dictionary.
/// </summary>
/// <remarks>
/// <c>DDRUMMonitor.AddAttributeForKey</c>, <c>AddViewAttributeForKey</c>,
/// <c>AddFeatureFlagEvaluationWithName</c> and the two <c>AddAttributeForKey</c> members on Logs all
/// take a bare <see cref="NSObject"/>, but <c>DatadogNet.iOS</c>'s <c>DatadogAttributes</c> only
/// exposes the dictionary form — its per-value <c>ToNSObject</c> is private in 3.x as it was in 2.x.
/// Rather than reimplement the conversion here and let the two drift, this round-trips through the
/// public dictionary API.
/// <para>
/// One <see cref="NSDictionary"/> per call, on a path that runs when an app sets a global or
/// view-scoped attribute or records a feature flag — not per event. Making the per-value converter
/// public upstream removes it.
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
