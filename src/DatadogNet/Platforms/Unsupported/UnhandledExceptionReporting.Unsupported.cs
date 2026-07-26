namespace DatadogNet;

public static partial class UnhandledExceptionReporting
{
    /// <remarks>
    /// Nothing beyond the shared AppDomain and TaskScheduler hooks: reporting is a no-op on this
    /// head anyway — every call lands in the neutral implementation and is dropped — so a
    /// platform-specific hook would be wiring with nothing on the other end. The shared hooks stay
    /// attached so the behaviour matches the supported heads' shape, which is what lets a unit
    /// test drive Enable() without conditionals.
    /// </remarks>
    private static partial void EnablePlatformHooks()
    {
    }
}
