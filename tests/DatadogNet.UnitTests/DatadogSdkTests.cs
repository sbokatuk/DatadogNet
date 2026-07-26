using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// <see cref="IDatadogSdk"/>: the lifecycle surface as a service.
/// </summary>
public class DatadogSdkTests
{
    [Fact]
    public void AddDatadog_registers_the_sdk_service()
    {
        var services = new ServiceCollection().AddDatadog();

        using var provider = services.BuildServiceProvider();

        Assert.Same(DatadogSdk.Instance, provider.GetRequiredService<IDatadogSdk>());
    }

    [Fact]
    public void A_registered_fake_wins_over_the_real_sdk()
    {
        // The whole reason the interface exists: "we call SetUser on sign-in" as a unit test.
        var services = new ServiceCollection();
        var fake = new FakeSdk();
        services.AddSingleton<IDatadogSdk>(fake);

        services.AddDatadog();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDatadogSdk>().SetUser("user-1", name: "Jo");

        Assert.Equal("user-1", fake.LastUserId);
    }

    [Fact]
    public void The_default_implementation_delegates_to_the_static_facade()
    {
        IDatadogSdk sdk = DatadogSdk.Instance;

        // Neutral head: unsupported, uninitialised, features disabled - exactly the static's view.
        Assert.Equal(Datadog.IsSupported, sdk.IsSupported);
        Assert.Equal(Datadog.IsInitialized, sdk.IsInitialized);
        Assert.Same(Datadog.Rum, sdk.Rum);
        Assert.Same(Datadog.Logs, sdk.Logs);
        Assert.Same(Datadog.Tracer, sdk.Tracer);
        Assert.Same(Datadog.SessionReplay, sdk.SessionReplay);
    }

    [Fact]
    public void The_default_implementation_keeps_the_facades_validation_contract()
    {
        IDatadogSdk sdk = DatadogSdk.Instance;

        // Initialize validates even where it cannot initialise - the one deliberate exception to
        // "nothing throws", and it must survive the trip through the interface.
        Assert.Throws<ArgumentException>(() => sdk.Initialize(new DatadogConfiguration
        {
            ClientToken = string.Empty,
            Env = "test",
        }));

        // And everything else stays throw-free on an unsupported head.
        sdk.SetTrackingConsent(TrackingConsent.Granted);
        sdk.SetUser("id");
        sdk.AddUserExtraInfo(new Dictionary<string, object?> { ["plan"] = "pro" });
        sdk.ClearUser();
        sdk.SetAccount("acct");
        sdk.ClearAccount();
        sdk.ClearAllData();
        sdk.Stop();
    }

    private sealed class FakeSdk : IDatadogSdk
    {
        public string? LastUserId { get; private set; }

        public bool IsSupported => true;

        public bool IsInitialized => true;

        public IRumMonitor Rum => throw new NotSupportedException();

        public IDatadogLogs Logs => throw new NotSupportedException();

        public IDatadogLogger Logger => throw new NotSupportedException();

        public IDatadogTracer Tracer => throw new NotSupportedException();

        public ISessionReplay SessionReplay => throw new NotSupportedException();

        public DatadogVerbosity Verbosity { get; set; }

        public void Initialize(DatadogConfiguration configuration)
        {
        }

        public void SetTrackingConsent(TrackingConsent consent)
        {
        }

        public void SetUser(
            string id,
            string? name = null,
            string? email = null,
            IReadOnlyDictionary<string, object?>? extraInfo = null) => LastUserId = id;

        public void AddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
        {
        }

        public void ClearUser()
        {
        }

        public void SetAccount(
            string id,
            string? name = null,
            IReadOnlyDictionary<string, object?>? extraInfo = null)
        {
        }

        public void AddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
        {
        }

        public void ClearAccount()
        {
        }

        public void ClearAllData()
        {
        }

        public void Stop()
        {
        }
    }
}
