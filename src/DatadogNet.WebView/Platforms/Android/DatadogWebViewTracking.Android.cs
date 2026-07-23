using NativeWebViewTracking = Com.Datadog.Android.Webview.WebViewTracking;

namespace DatadogNet;

public static partial class DatadogWebViewTracking
{
    private static partial void PlatformEnable(
        object platformWebView,
        IReadOnlyList<string> allowedHosts,
        float logsSampleRate)
    {
        var webView = platformWebView as Android.Webkit.WebView
            ?? throw new ArgumentException(
                $"Expected an Android.Webkit.WebView, but got a {platformWebView.GetType().FullName}. " +
                "From MAUI this is webView.Handler?.PlatformView.",
                nameof(platformWebView));

        NativeWebViewTracking.Enable(webView, [.. allowedHosts], logsSampleRate);
    }

    private static partial void PlatformDisable(object platformWebView)
    {
        // dd-sdk-android has no disable: the bridge is a JavascriptInterface attached to the
        // WebView, and it goes when the WebView does. dd-sdk-ios needs an explicit teardown because
        // its bridge is a WKUserContentController script message handler, which WKWebView retains
        // strongly and which therefore outlives the page unless removed.
        _ = platformWebView;
    }
}
