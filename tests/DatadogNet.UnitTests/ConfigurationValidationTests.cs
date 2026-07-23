using DatadogNet;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// Covers the validation <see cref="Datadog.Initialize"/> does before it touches a platform.
/// </summary>
/// <remarks>
/// These run against the neutral head, where <c>PlatformInitialize</c> is a no-op — which is
/// exactly the point. Validation happens before the initialisation gate and before any native call,
/// so all of it is reachable without a device.
/// <para>
/// Each failure here is one a developer would otherwise meet as silence: the SDK accepts a bad
/// client token or a bad application id happily and uploads to nowhere, and there is no later point
/// at which that becomes visible.
/// </para>
/// </remarks>
public class ConfigurationValidationTests
{
    private static RumOptions Rum => new() { ApplicationId = "app-id" };

    [Fact]
    public void Rejects_a_null_configuration() =>
        Assert.Throws<ArgumentNullException>(() => Datadog.Initialize(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_client_token(string token)
    {
        var error = Assert.Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration { ClientToken = token, Env = "test" }));

        // The message names where to find one, because the commonest cause of an empty upload is
        // someone having pasted an API key instead.
        Assert.Contains("client token", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_environment(string env)
    {
        var error = Assert.Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration { ClientToken = "token", Env = env }));

        Assert.Contains("environment", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_RUM_without_an_application_id(string applicationId)
    {
        var error = Assert.Throws<ArgumentException>(() => Datadog.Initialize(
            new DatadogConfiguration
            {
                ClientToken = "token",
                Env = "test",
                Rum = new RumOptions { ApplicationId = applicationId },
            }));

        Assert.Contains("ApplicationId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_Session_Replay_without_RUM()
    {
        // A replay is attached to a RUM session. Enabling it alone records nothing at all, and the
        // native SDKs do not complain - they simply never produce a replay.
        var error = Assert.Throws<ArgumentException>(() => Datadog.Initialize(
            new DatadogConfiguration
            {
                ClientToken = "token",
                Env = "test",
                SessionReplay = new SessionReplayOptions(),
            }));

        Assert.Contains("requires RUM", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(100.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Rejects_a_sample_rate_outside_nought_to_a_hundred(float rate)
    {
        // Both SDKs take a percentage, and both clamp silently. A rate of 1000 meaning "100%" and a
        // rate of -1 meaning "0%" are the kind of thing that is only ever noticed as a bill.
        Assert.Throws<ArgumentException>(() => Datadog.Initialize(
            new DatadogConfiguration
            {
                ClientToken = "token",
                Env = "test",
                Rum = new RumOptions { ApplicationId = "app-id", SessionSampleRate = rate },
            }));

        Assert.Throws<ArgumentException>(() => Datadog.Initialize(
            new DatadogConfiguration
            {
                ClientToken = "token",
                Env = "test",
                Trace = new TraceOptions { SampleRate = rate },
            }));

        Assert.Throws<ArgumentException>(() => Datadog.Initialize(
            new DatadogConfiguration
            {
                ClientToken = "token",
                Env = "test",
                Rum = Rum,
                SessionReplay = new SessionReplayOptions { SampleRate = rate },
            }));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(50f)]
    [InlineData(100f)]
    public void Accepts_the_whole_valid_range(float rate)
    {
        // 0 is meaningful - it is how you keep a feature configured but silent - so it must not be
        // rejected as "unset".
        try
        {
            var error = Record.Exception(() => Datadog.Initialize(
                new DatadogConfiguration
                {
                    ClientToken = "token",
                    Env = "test",
                    Rum = new RumOptions { ApplicationId = "app-id", SessionSampleRate = rate },
                }));

            Assert.Null(error);
        }
        finally
        {
            // In a finally, because this is the only test that leaves the static façade
            // initialised. A failing assertion above would otherwise hand every later test a
            // configured SDK, turning one failure into a cascade that hides its own cause.
            Datadog.Stop();
        }
    }
}
