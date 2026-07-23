using DatadogObjc;
using Foundation;

namespace DatadogNet;

/// <summary>
/// Converts one attribute value, for the native members that take a single value rather than a
/// dictionary.
/// </summary>
/// <remarks>
/// A thin alias over <c>DatadogNet.iOS</c>'s own converter, kept so the two platform
/// implementations name the same thing the same way - the Android one has more to do.
/// </remarks>
internal static class NativeAttributes
{
    /// <summary>Converts a single value to its Objective-C form.</summary>
    /// <param name="key">The attribute name, used only to name the value in any error.</param>
    /// <param name="value">The value.</param>
    internal static NSObject Single(string key, object? value) =>
        DatadogAttributes.ToNSObject(value, key);
}
