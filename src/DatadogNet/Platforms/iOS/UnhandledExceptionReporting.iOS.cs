using ObjCRuntime;

namespace DatadogNet;

public static partial class UnhandledExceptionReporting
{
    /// <remarks>
    /// A managed exception thrown inside a UIKit callback is marshalled at the native boundary,
    /// and — depending on the marshalling mode — can abort the process or unwind as an
    /// Objective-C exception without ever reaching <c>AppDomain.UnhandledException</c>. This hook
    /// sees it at the boundary, while it still has its .NET type and managed frames. When the
    /// same exception later reaches the AppDomain hook too, the reported-set in the shared file
    /// keeps it to one RUM error. The event's <c>ExceptionMode</c> is read, never set: observing
    /// the failure must not change how the runtime handles it. Compiled for the Mac Catalyst heads
    /// too — Catalyst is UIKit and marshals at the same boundary.
    /// </remarks>
    private static partial void EnablePlatformHooks() =>
        Runtime.MarshalManagedException += (_, args) =>
        {
            if (args.Exception is { } exception)
            {
                Report(exception, fatal: args.ExceptionMode == MarshalManagedExceptionMode.Abort);
            }
        };
}
