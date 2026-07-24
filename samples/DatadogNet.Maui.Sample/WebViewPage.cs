namespace DatadogNet.Maui.Sample;

/// <summary>
/// A page hosting a web view with the Datadog bridge installed, from <c>DatadogNet.WebView</c>.
/// </summary>
/// <remarks>
/// The bridge is one call against the platform web view, which MAUI exposes through the handler
/// once the view is loaded. For anything to actually come across, the page inside must run the
/// Datadog Browser SDK and its host must be on the allowlist — this sample's page does not, so
/// what this demonstrates is the wiring: install on load, remove on unload, no platform code.
/// </remarks>
public sealed class WebViewPage : ContentPage
{
    private readonly WebView webView;

    public WebViewPage()
    {
        Title = "Web view";

        DatadogTracking.SetViewName(this, "WebView");

        webView = new WebView { Source = "https://example.com/" };

        // The platform view exists once the handler has connected, which Loaded guarantees.
        // Unloaded mirrors it: the bridge holds a reference to the web view, so removing it is
        // what lets the page die with its view.
        webView.Loaded += OnWebViewLoaded;
        webView.Unloaded += OnWebViewUnloaded;

        var layout = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            ],
        };

        layout.Add(
            new Label
            {
                Padding = 12,
                FontSize = 13,
                Text = "The Datadog bridge is installed on this web view. A page running the "
                       + "Datadog Browser SDK on an allowlisted host would report into the "
                       + "surrounding native session; example.com does not, so nothing crosses "
                       + "here - the point is the wiring.",
            },
            0,
            0);
        layout.Add(webView, 0, 1);

        Content = layout;
    }

    private void OnWebViewLoaded(object? sender, EventArgs e)
    {
        if (webView.Handler?.PlatformView is { } platform)
        {
            DatadogWebViewTracking.Enable(platform, ["example.com"]);
        }
    }

    private void OnWebViewUnloaded(object? sender, EventArgs e)
    {
        if (webView.Handler?.PlatformView is { } platform)
        {
            DatadogWebViewTracking.Disable(platform);
        }
    }
}
