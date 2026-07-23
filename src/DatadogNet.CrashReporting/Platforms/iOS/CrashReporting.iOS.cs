using DatadogCrashReporting;

namespace DatadogNet;

public static partial class CrashReporting
{
    private static partial void PlatformEnable() => DDCrashReporter.Enable();
}
