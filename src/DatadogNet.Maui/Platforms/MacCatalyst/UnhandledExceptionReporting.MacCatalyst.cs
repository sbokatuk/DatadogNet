using ObjCRuntime;

namespace DatadogNet.Maui;

internal static partial class UnhandledExceptionReporting
{
    /// <remarks>
    /// Byte-for-byte the Platforms/iOS implementation, for the reason the sibling
    /// MauiAppBuilderExtensions file records: Catalyst is UIKit and marshals managed exceptions at
    /// the same boundary, but this UseMaui+SingleProject project cannot ride the repository's
    /// usual "Catalyst compiles Platforms/iOS" arrangement, because MAUI's single-project targets
    /// remove Platforms/iOS/** from every non-iOS head after our own item groups have run.
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
