namespace DatadogNet.Maui;

internal static partial class UnhandledExceptionReporting
{
    /// <remarks>
    /// Nothing beyond the shared AppDomain and TaskScheduler hooks: reporting is a no-op on
    /// Windows anyway - the core package's neutral implementation drops every call - so a
    /// WinUI-specific hook would be wiring with nothing on the other end.
    /// </remarks>
    private static partial void EnablePlatformHooks()
    {
    }
}
