using Android.Runtime;

namespace DatadogNet;

public static partial class UnhandledExceptionReporting
{
    /// <remarks>
    /// <c>UnhandledExceptionRaiser</c> fires when a managed exception is about to propagate into
    /// Java — a thrown exception leaving an Android callback — which, depending on the runtime's
    /// mood and version, does not reliably arrive at <c>AppDomain.UnhandledException</c> as well.
    /// When both fire for the same exception, the reported-set in the shared file keeps it to one
    /// RUM error. <c>args.Handled</c> is deliberately left alone: observing the failure must not
    /// change whether the app dies of it.
    /// </remarks>
    private static partial void EnablePlatformHooks() =>
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            Report(args.Exception, fatal: true);
}
