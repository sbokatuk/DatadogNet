using System.Diagnostics;

namespace DatadogNet;

/// <summary>
/// Which <see cref="ActivitySource"/>s the bridge forwards, and how their activities become spans.
/// </summary>
/// <remarks>
/// Forwarding is opt-in per source, on purpose. A modern .NET process emits activities from
/// everywhere — <c>HttpClient</c>, EF Core, gRPC, MassTransit, half of NuGet — and every span the
/// bridge forwards is an event Datadog ingests. Naming the sources you mean is the difference
/// between "my checkout flow is traced" and a bill.
/// </remarks>
public sealed class DatadogActivityBridgeOptions
{
    /// <summary>
    /// The <see cref="ActivitySource.Name"/>s to forward, matched exactly.
    /// </summary>
    /// <remarks>
    /// At least one source (or a <see cref="ShouldListen"/> predicate) is required —
    /// <see cref="DatadogActivityBridge.Start(DatadogActivityBridgeOptions)"/> throws otherwise,
    /// because a bridge listening to nothing is always a configuration mistake.
    /// </remarks>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>
    /// Decides per source instead of by name. When set, <see cref="Sources"/> is ignored.
    /// </summary>
    /// <remarks>
    /// For prefix matching and the like: <c>source => source.Name.StartsWith ("MyCompany.")</c>.
    /// Called once per source as it appears, not per activity.
    /// </remarks>
    public Func<ActivitySource, bool>? ShouldListen { get; init; }

    /// <summary>
    /// Names the Datadog span for an activity. Defaults to <see cref="Activity.OperationName"/>.
    /// </summary>
    /// <remarks>
    /// Keep it low-cardinality — it is Datadog's operation name, the thing traces group by. The
    /// per-activity detail (<see cref="Activity.DisplayName"/>) already lands on the span as
    /// <c>resource.name</c> when it differs.
    /// </remarks>
    public Func<Activity, string> OperationName { get; init; } = static activity => activity.OperationName;
}
