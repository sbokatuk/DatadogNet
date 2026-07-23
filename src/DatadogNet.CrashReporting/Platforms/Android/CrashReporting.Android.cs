using Com.Datadog.Android.Ndk;

namespace DatadogNet;

public static partial class CrashReporting
{
    private static partial void PlatformEnable() => NdkCrashReports.Enable();
}
