namespace DatadogNet;

public static partial class DatadogWebViewTracking
{
    private static partial void PlatformEnable(
        object platformWebView,
        IReadOnlyList<string> allowedHosts,
        float logsSampleRate)
    {
    }

    private static partial void PlatformDisable(object platformWebView)
    {
    }
}
