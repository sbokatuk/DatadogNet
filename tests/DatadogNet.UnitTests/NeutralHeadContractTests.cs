using DatadogNet;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// Covers the promise the neutral head makes: nothing throws, ever.
/// </summary>
/// <remarks>
/// This is what lets a multi-headed MAUI app call the façade from shared code with no conditionals —
/// on Windows the whole API is present and does nothing, while Mac Catalyst gets the real SDK via
/// the <c>DatadogNet.Mac</c> bindings. It is also the behaviour before
/// <see cref="Datadog.Initialize"/> on every platform.
/// <para>
/// The rule has one deliberate exception, asserted below: <see cref="Datadog.Initialize"/> validates
/// even where it cannot initialise, so a configuration mistake is caught on the developer's desktop
/// head rather than only on a device.
/// </para>
/// </remarks>
public class NeutralHeadContractTests
{
    [Fact]
    public void Reports_itself_unsupported_rather_than_pretending()
    {
        Assert.False(Datadog.IsSupported);
        Assert.False(Datadog.Rum.IsEnabled);
        Assert.False(Datadog.Logs.IsEnabled);
        Assert.False(Datadog.Tracer.IsEnabled);
    }

    [Fact]
    public void Drives_the_whole_RUM_surface_without_throwing()
    {
        var error = Record.Exception(() =>
        {
            using var view = Datadog.Rum.StartView("key", "name");

            view.AddAttributes(new Dictionary<string, object?> { ["a"] = 1 });
            view.RemoveAttributes(["a"]);
            view.AddLoadingTime();
            view.AddLoadingTime(overwrite: true);

            Datadog.Rum.AddAction(RumActionType.Tap, "tap");
            Datadog.Rum.StartAction(RumActionType.Scroll, "scroll");
            Datadog.Rum.StopAction(RumActionType.Scroll, "scroll");
            Datadog.Rum.AddError(new InvalidOperationException("boom"));
            Datadog.Rum.AddError("message");
            Datadog.Rum.StartResource("r", RumHttpMethod.Get, "https://example.com");
            Datadog.Rum.StopResource("r", 200);
            Datadog.Rum.AddTiming("timing");
            Datadog.Rum.AddFeatureFlagEvaluation("flag", true);
            Datadog.Rum.AddAttribute("k", "v");
            Datadog.Rum.RemoveAttribute("k");
            Datadog.Rum.ReportAppFullyDisplayed();
            Datadog.Rum.StopSession();

            view.Stop();
        });

        Assert.Null(error);
    }

    [Fact]
    public void Drives_logging_and_tracing_without_throwing()
    {
        var error = Record.Exception(() =>
        {
            Datadog.Logger.Info("hello");
            Datadog.Logger.Error("bad", new InvalidOperationException("boom"));

            using var span = Datadog.Tracer.StartSpan("op");
            span.SetTag("s", "v");
            span.SetTag("d", 1.5);
            span.SetTag("b", true);
            span.SetError(new InvalidOperationException("boom"));

            using (span.Activate())
            {
            }

            _ = Datadog.Tracer.Inject(span);
            span.Finish();
        });

        Assert.Null(error);
    }

    [Fact]
    public async Task Answers_the_session_id_with_null_rather_than_hanging()
    {
        // A Task that never completes would deadlock a caller awaiting it on a desktop head, which
        // is a far worse failure than a null.
        Assert.Null(await Datadog.Rum.GetCurrentSessionIdAsync());
    }

    [Fact]
    public void Injects_no_headers_rather_than_empty_ones()
    {
        using var span = Datadog.Tracer.StartSpan("op");

        // An empty dictionary, not null: callers write `foreach (var h in Inject(span))` without
        // guarding, and the whole point of this head is that shared code needs no guards.
        var headers = Datadog.Tracer.Inject(span);

        Assert.NotNull(headers);
        Assert.Empty(headers);
    }

    [Fact]
    public void Still_validates_the_configuration_it_cannot_act_on()
    {
        // The exception to "nothing throws", and it earns its place: a developer running the
        // Windows head should find out about an empty environment then, not when someone runs the
        // iOS build three days later.
        Assert.Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration { ClientToken = "", Env = "test" }));
    }
}
