using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DatadogNet;

/// <summary>
/// Forwards <see cref="Activity"/> spans from named <see cref="ActivitySource"/>s into Datadog.
/// </summary>
/// <remarks>
/// <see cref="IDatadogTracer"/> is Datadog's manual span API; the .NET ecosystem's is
/// <see cref="ActivitySource"/>, and libraries that are already instrumented emit through it.
/// This bridge is the join: an <see cref="ActivityListener"/> starts a Datadog span when a
/// forwarded activity starts and finishes it when the activity stops, carrying tags, the
/// display name (as <c>resource.name</c>) and error status across. Parentage is preserved for
/// activities the bridge saw; an activity whose parent came from an unforwarded source starts a
/// new Datadog trace.
/// <code>
/// using var bridge = DatadogActivityBridge.Start (new DatadogActivityBridgeOptions {
///     Sources = ["MyCompany.Checkout", "MyCompany.Sync"],
/// });
/// </code>
/// Start it once, after <see cref="Datadog.Initialize"/>; dispose to stop forwarding. Like the
/// rest of the façade it never throws because Datadog is unavailable — activities forwarded
/// before initialisation, or on a platform with no Datadog, are dropped.
/// <para>
/// What this deliberately is not: an OpenTelemetry exporter. Spans go through the same
/// <see cref="IDatadogTracer"/> as hand-written ones — sampled by
/// <see cref="TraceOptions.SampleRate"/>, correlated with RUM the same way — rather than through
/// a parallel pipeline with its own configuration to reconcile.
/// </para>
/// </remarks>
public sealed class DatadogActivityBridge : IDisposable
{
    private readonly ActivityListener listener;

    private readonly Func<IDatadogTracer> tracer;

    private readonly DatadogActivityBridgeOptions options;

    /// <summary>
    /// The live Datadog span for each in-flight forwarded activity. Weak on the activity, so an
    /// activity that is never stopped — abandoned by its own library — leaks no span here.
    /// </summary>
    private readonly ConditionalWeakTable<Activity, IDatadogSpan> spans = [];

    private DatadogActivityBridge(DatadogActivityBridgeOptions options, Func<IDatadogTracer> tracer)
    {
        this.options = options;
        this.tracer = tracer;

        var names = new HashSet<string>(options.Sources, StringComparer.Ordinal);
        var shouldListen = options.ShouldListen ?? (source => names.Contains(source.Name));

        listener = new ActivityListener
        {
            ShouldListenTo = shouldListen,

            // AllData, not AllDataAndRecorded: the bridge needs tags and status, but it does its
            // own forwarding - setting Recorded would tell *other* consumers of the activity that
            // something downstream persists it, which is not this bridge's claim to make.
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStarted = OnStarted,
            ActivityStopped = OnStopped,
        };

        ActivitySource.AddActivityListener(listener);
    }

    /// <summary>
    /// Starts forwarding, through <see cref="Datadog.Tracer"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> names no sources and has no predicate.
    /// </exception>
    public static DatadogActivityBridge Start(DatadogActivityBridgeOptions options)
    {
        Validate(options);
        return new DatadogActivityBridge(options, static () => Datadog.Tracer);
    }

    /// <summary>
    /// Starts forwarding into a specific tracer — a decorator, or a fake in a test.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> names no sources and has no predicate.
    /// </exception>
    public static DatadogActivityBridge Start(DatadogActivityBridgeOptions options, IDatadogTracer tracer)
    {
        Validate(options);
        ArgumentNullException.ThrowIfNull(tracer);
        return new DatadogActivityBridge(options, () => tracer);
    }

    /// <summary>Stops forwarding. In-flight activities finish unbridged.</summary>
    public void Dispose() => listener.Dispose();

    private static void Validate(DatadogActivityBridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Sources.Count == 0 && options.ShouldListen is null)
        {
            throw new ArgumentException(
                "The bridge would listen to nothing: name at least one ActivitySource in Sources, " +
                "or supply a ShouldListen predicate.",
                nameof(options));
        }
    }

    private void OnStarted(Activity activity)
    {
        var current = tracer();

        // Before Initialize, or with Trace not enabled, there is nothing to forward into - and
        // skipping entirely (rather than starting no-op spans) keeps the table empty. An activity
        // started in that window is dropped even if Trace comes up before it stops, matching the
        // facade-wide pre-initialisation contract.
        if (!current.IsEnabled)
        {
            return;
        }

        var parent = activity.Parent is { } parentActivity
            && spans.TryGetValue(parentActivity, out var parentSpan)
                ? parentSpan
                : null;

        spans.Add(activity, current.StartSpan(options.OperationName(activity), parent));
    }

    private void OnStopped(Activity activity)
    {
        if (!spans.TryGetValue(activity, out var span))
        {
            return;
        }

        spans.Remove(activity);

        foreach (var (key, value) in activity.EnumerateTagObjects())
        {
            SetTag(span, key, value);
        }

        if (activity.DisplayName is { Length: > 0 } displayName && displayName != activity.OperationName)
        {
            span.SetTag("resource.name", displayName);
        }

        if (activity.Status == ActivityStatusCode.Error)
        {
            // The activity API records "it failed" and optionally why, not an exception object -
            // libraries attach exceptions as events, in shapes that vary by library. The status
            // is the reliable part, so that is what marks the span errored.
            span.SetError("Error", activity.StatusDescription ?? options.OperationName(activity));
        }

        span.Finish();
    }

    private static void SetTag(IDatadogSpan span, string key, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case bool flag:
                span.SetTag(key, flag);
                break;
            case double number:
                span.SetTag(key, number);
                break;
            case int or long or float or decimal or short or byte:
                span.SetTag(key, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case string text:
                span.SetTag(key, text);
                break;
            default:
                span.SetTag(key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }
}
