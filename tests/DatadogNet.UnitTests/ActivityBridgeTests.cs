using System.Diagnostics;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// <see cref="DatadogActivityBridge"/>: Activity spans becoming Datadog spans.
/// </summary>
/// <remarks>
/// Driven through real <see cref="ActivitySource"/> plumbing — the bridge's listener is what makes
/// <c>StartActivity</c> return non-null at all — against a recording tracer, so what is asserted
/// is the whole path a library's instrumentation takes, not the bridge's internals.
/// </remarks>
public class ActivityBridgeTests
{
    [Fact]
    public void Forwards_only_the_named_sources()
    {
        var tracer = new RecordingTracer();
        using var named = new ActivitySource("Bridge.Named");
        using var other = new ActivitySource("Bridge.Other");
        using var bridge = DatadogActivityBridge.Start(
            new DatadogActivityBridgeOptions { Sources = ["Bridge.Named"] },
            tracer);

        using (var activity = named.StartActivity("forwarded"))
        {
            Assert.NotNull(activity);
        }

        // The unforwarded source has no listener, so its StartActivity returns null - the
        // ecosystem's own "nobody is listening" fast path, and exactly what keeps unnamed
        // sources free.
        using (var activity = other.StartActivity("ignored"))
        {
            Assert.Null(activity);
        }

        var span = Assert.Single(tracer.Spans);
        Assert.Equal("forwarded", span.OperationName);
        Assert.True(span.Finished);
    }

    [Fact]
    public void Carries_tags_display_name_error_and_parentage()
    {
        var tracer = new RecordingTracer();
        using var source = new ActivitySource("Bridge.Detail");
        using var bridge = DatadogActivityBridge.Start(
            new DatadogActivityBridgeOptions { Sources = ["Bridge.Detail"] },
            tracer);

        using (var parent = source.StartActivity("checkout"))
        {
            using (var child = source.StartActivity("payment"))
            {
                child!.SetTag("order.id", 42);
                child.SetTag("gateway", "stripe");
                child.DisplayName = "payment POST /charge";
                child.SetStatus(ActivityStatusCode.Error, "card declined");
            }
        }

        Assert.Equal(2, tracer.Spans.Count);
        var childSpan = tracer.Spans[1];
        var parentSpan = tracer.Spans[0];

        Assert.Same(parentSpan, childSpan.Parent);
        Assert.Equal(42d, childSpan.Tags["order.id"]);
        Assert.Equal("stripe", childSpan.Tags["gateway"]);
        Assert.Equal("payment POST /charge", childSpan.Tags["resource.name"]);
        Assert.Equal("card declined", childSpan.ErrorMessage);
        Assert.True(childSpan.Finished);
        Assert.Null(parentSpan.ErrorMessage);
    }

    [Fact]
    public void Dispose_stops_forwarding()
    {
        var tracer = new RecordingTracer();
        using var source = new ActivitySource("Bridge.Stopped");
        var bridge = DatadogActivityBridge.Start(
            new DatadogActivityBridgeOptions { Sources = ["Bridge.Stopped"] },
            tracer);
        bridge.Dispose();

        using var activity = source.StartActivity("after");

        Assert.Null(activity);
        Assert.Empty(tracer.Spans);
    }

    [Fact]
    public void A_disabled_tracer_forwards_nothing()
    {
        // Trace not enabled (or the SDK not initialised) is the facade-wide "dropped, not thrown"
        // contract; the bridge must not build up spans that will never go anywhere.
        var tracer = new RecordingTracer { IsEnabled = false };
        using var source = new ActivitySource("Bridge.Disabled");
        using var bridge = DatadogActivityBridge.Start(
            new DatadogActivityBridgeOptions { Sources = ["Bridge.Disabled"] },
            tracer);

        using (source.StartActivity("dropped"))
        {
        }

        Assert.Empty(tracer.Spans);
    }

    [Fact]
    public void Listening_to_nothing_is_a_configuration_error()
    {
        Assert.Throws<ArgumentException>(
            () => DatadogActivityBridge.Start(new DatadogActivityBridgeOptions()));
    }

    [Fact]
    public void A_predicate_overrides_the_name_list()
    {
        var tracer = new RecordingTracer();
        using var source = new ActivitySource("MyCompany.Anything");
        using var bridge = DatadogActivityBridge.Start(
            new DatadogActivityBridgeOptions
            {
                ShouldListen = s => s.Name.StartsWith("MyCompany.", StringComparison.Ordinal),
            },
            tracer);

        using (source.StartActivity("prefixed"))
        {
        }

        Assert.Equal("prefixed", Assert.Single(tracer.Spans).OperationName);
    }

    private sealed class RecordingTracer : IDatadogTracer
    {
        public List<RecordingSpan> Spans { get; } = [];

        public bool IsEnabled { get; init; } = true;

        public IDatadogSpan? ActiveSpan => null;

        public IDatadogSpan StartSpan(
            string operationName,
            IDatadogSpan? parent = null,
            IReadOnlyDictionary<string, object?>? tags = null)
        {
            var span = new RecordingSpan(operationName, parent);
            Spans.Add(span);
            return span;
        }

        public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span) =>
            new Dictionary<string, string>();
    }

    private sealed class RecordingSpan(string operationName, IDatadogSpan? parent) : IDatadogSpan
    {
        public string OperationName { get; } = operationName;

        public IDatadogSpan? Parent { get; } = parent;

        public Dictionary<string, object?> Tags { get; } = [];

        public string? ErrorMessage { get; private set; }

        public bool Finished { get; private set; }

        public string TraceId => "trace";

        public string SpanId => "span";

        public void SetTag(string key, string value) => Tags[key] = value;

        public void SetTag(string key, double value) => Tags[key] = value;

        public void SetTag(string key, bool value) => Tags[key] = value;

        public void SetError(Exception exception) => ErrorMessage = exception.Message;

        public void SetError(string kind, string message, string? stack = null) => ErrorMessage = message;

        public void Log(IReadOnlyDictionary<string, object?> fields)
        {
        }

        public IDisposable Activate() => throw new NotSupportedException();

        public void Finish() => Finished = true;

        public void Dispose() => Finish();
    }
}
