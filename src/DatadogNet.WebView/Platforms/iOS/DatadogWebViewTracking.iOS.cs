using DatadogWebViewTrackingFramework = DatadogWebViewTracking.DDWebViewTracking;
using Foundation;
using WebKit;

namespace DatadogNet;

public static partial class DatadogWebViewTracking
{
    private static partial void PlatformEnable(
        object platformWebView,
        IReadOnlyList<string> allowedHosts,
        float logsSampleRate)
    {
        var webView = platformWebView as WKWebView
            ?? throw new ArgumentException(
                $"Expected a WebKit.WKWebView, but got a {platformWebView.GetType().FullName}. " +
                "From MAUI this is webView.Handler?.PlatformView.",
                nameof(platformWebView));

        DatadogWebViewTrackingFramework.EnableWithWebView(
            webView,
            // The array is spelled out rather than collection-expressed: NSSet<T> has both a
            // params T[] and an NSMutableSet<T> constructor, and a bare collection expression is
            // ambiguous between them.
            new NSSet<NSString>(allowedHosts.Select(host => new NSString(host)).ToArray()),
            logsSampleRate);
    }

    private static partial void PlatformDisable(object platformWebView)
    {
        if (platformWebView is WKWebView webView)
        {
            DatadogWebViewTrackingFramework.DisableWithWebView(webView);
        }
    }
}
