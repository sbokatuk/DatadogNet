namespace DatadogNet;

/// <summary>
/// Bridges RUM events and logs out of a web view, so hybrid content joins the same session as the
/// native app around it.
/// </summary>
/// <remarks>
/// Requires the Datadog Browser SDK to be running inside the page. This side only installs the
/// bridge; the page has to be instrumented for anything to come across, and its host has to be on
/// the allowlist or the bridge refuses it.
/// <para>
/// From a MAUI page, the platform web view comes off the handler:
/// </para>
/// <code>
/// void OnWebViewLoaded (object? sender, EventArgs e)
/// {
///     if (MyWebView.Handler?.PlatformView is { } platform)
///         DatadogWebViewTracking.Enable (platform, ["example.com"]);
/// }
/// </code>
/// A separate package because it is opt-in on both platforms and pulls a framework an app without
/// a web view has no use for.
/// </remarks>
public static partial class DatadogWebViewTracking
{
    /// <summary>
    /// Starts bridging events out of a web view.
    /// </summary>
    /// <param name="platformWebView">
    /// The platform web view — an <c>Android.Webkit.WebView</c> or a <c>WebKit.WKWebView</c>. From
    /// MAUI, <c>webView.Handler?.PlatformView</c>.
    /// </param>
    /// <param name="allowedHosts">
    /// Hosts the bridge accepts events from. Matched by suffix, so <c>example.com</c> also covers
    /// <c>app.example.com</c>. An allowlist rather than a filter: the bridge lets page JavaScript
    /// write into your RUM session, and you do not want that available to whatever a redirect
    /// lands on.
    /// </param>
    /// <param name="logsSampleRate">Percentage of the page's logs kept, 0 to 100.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="platformWebView"/> is not this platform's web view type.
    /// </exception>
    /// <remarks>
    /// Typed as <see cref="object"/> so one signature serves both platforms and shared MAUI code
    /// can call it. The cast is checked, and a wrong type is an <see cref="ArgumentException"/>
    /// naming what was passed rather than a silent no-op — this is set-up code that runs once, and
    /// a web view that quietly reports nothing is the failure worth being loud about.
    /// </remarks>
    public static void Enable(
        object platformWebView,
        IEnumerable<string> allowedHosts,
        float logsSampleRate = 100)
    {
        ArgumentNullException.ThrowIfNull(platformWebView);
        ArgumentNullException.ThrowIfNull(allowedHosts);

        var hosts = allowedHosts as IReadOnlyList<string> ?? [.. allowedHosts];

        if (hosts.Count == 0)
        {
            throw new ArgumentException(
                "At least one allowed host is required. An empty list would install the bridge and " +
                "then reject every event it received.",
                nameof(allowedHosts));
        }

        PlatformEnable(platformWebView, hosts, logsSampleRate);
    }

    /// <summary>
    /// Stops bridging events out of a web view.
    /// </summary>
    /// <param name="platformWebView">The web view <see cref="Enable"/> was called with.</param>
    /// <remarks>
    /// Call on teardown. The bridge holds a reference to the web view, so leaving it installed
    /// keeps a page alive after its own view has gone.
    /// </remarks>
    public static void Disable(object platformWebView)
    {
        ArgumentNullException.ThrowIfNull(platformWebView);

        PlatformDisable(platformWebView);
    }

    private static partial void PlatformEnable(
        object platformWebView,
        IReadOnlyList<string> allowedHosts,
        float logsSampleRate);

    private static partial void PlatformDisable(object platformWebView);
}
