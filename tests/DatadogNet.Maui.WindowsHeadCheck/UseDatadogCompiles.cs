using DatadogNet;
using DatadogNet.Maui;

namespace DatadogNet.WindowsHeadCheck;

/// <summary>
/// Compiles the surface a windows-headed MAUI app actually touches, against the packed packages.
/// </summary>
/// <remarks>
/// Never executed — the stub head's runtime behaviour is a documented no-op covered by the unit
/// tests' neutral-head contract. What only a consumer compile can prove is the packaging: that
/// NuGet resolves a windows asset for <c>DatadogNet.Maui</c> at all (the pre-3.14.0.4 failure
/// mode was NU1202 at restore), and that <c>UseDatadog</c> and the types it needs are in it.
/// </remarks>
public static class UseDatadogCompiles
{
    /// <summary>The one-call setup, exactly as the README shows it.</summary>
    public static MauiApp Build() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<Application>()
            .UseDatadog(new DatadogConfiguration
            {
                ClientToken = "compile-check",
                Env = "compile-check",
                Rum = new RumOptions { ApplicationId = "compile-check" },
                Logs = new LogsOptions(),
            })
            .Build();

    /// <summary>The stub surface shared code branches on.</summary>
    public static bool ReportsUnsupported() => Datadog.IsSupported;
}
