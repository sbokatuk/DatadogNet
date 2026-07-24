using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// The DatadogNet.Extensions.DependencyInjection surface, against the neutral no-op head.
/// </summary>
/// <remarks>
/// These run without a device for the same reason the rest of this project does: the wiring is
/// platform-independent and compiles identically into every head, so exercising it against the
/// no-op implementation exercises the registrations, the provider and the handler plumbing —
/// everything except the native SDK itself, which the device tests cover.
/// </remarks>
public class DependencyInjectionTests
{
    [Fact]
    public void AddDatadog_registers_every_datadog_service()
    {
        var services = new ServiceCollection().AddDatadog();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRumMonitor>());
        Assert.NotNull(provider.GetRequiredService<IDatadogLogs>());
        Assert.NotNull(provider.GetRequiredService<IDatadogTracer>());
        Assert.NotNull(provider.GetRequiredService<ISessionReplay>());
        Assert.NotNull(provider.GetRequiredService<IDatadogLogger>());
    }

    [Fact]
    public void AddDatadog_keeps_an_existing_registration()
    {
        // TryAdd semantics are the contract that makes a test host's fake win; losing them would
        // silently replace an app's decorator with the real monitor.
        var services = new ServiceCollection();
        var fake = new FakeRumMonitor();
        services.AddSingleton<IRumMonitor>(fake);

        services.AddDatadog();

        using var provider = services.BuildServiceProvider();
        Assert.Same(fake, provider.GetRequiredService<IRumMonitor>());
    }

    [Fact]
    public void AddDatadog_with_configuration_initialises_and_registers()
    {
        // On the neutral head Initialize is a documented no-op, so what this asserts is the shape:
        // the overload accepts a configuration and still ends with every service resolvable.
        var services = new ServiceCollection().AddDatadog(new DatadogConfiguration
        {
            ClientToken = "token",
            Env = "test",
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IRumMonitor>());
    }

    [Fact]
    public void AddDatadog_logging_provider_forwards_without_throwing()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddDatadog());

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

        logger.LogInformation("order placed");
        logger.LogError(new InvalidOperationException("boom"), "order failed");
    }

    [Fact]
    public void AddDatadog_logging_provider_registers_once()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddDatadog().AddDatadog());

        var providers = services.Count(descriptor =>
            descriptor.ServiceType == typeof(ILoggerProvider)
            && descriptor.ImplementationType?.Name == "DatadogLoggerProvider");

        // TryAddEnumerable de-duplicates on the implementation type; a second AddDatadog must not
        // produce a second provider, or every log line would be reported twice.
        Assert.Equal(0, providers);
        Assert.Equal(
            1,
            services.Count(descriptor =>
                descriptor.ServiceType == typeof(ILoggerProvider)
                && descriptor.ImplementationFactory is not null));
    }

    [Fact]
    public void AddDatadogTracking_produces_a_working_client()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("datadog").AddDatadogTracking(options =>
        {
            options.TrackResources = false;
            options.OperationName = _ => "custom";
        });

        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("datadog");

        // Creating the client is what runs the handler factory; a broken registration throws here
        // rather than on the first request in an app.
        Assert.NotNull(client);
    }

    /// <summary>
    /// The smallest possible substitute: identity is all the TryAdd test needs from it.
    /// </summary>
    private sealed class FakeRumMonitor : IRumMonitor
    {
        public bool IsEnabled => false;

        public bool Debug { get; set; }

        public IRumViewScope StartView(
            string key,
            string? name = null,
            IReadOnlyDictionary<string, object?>? attributes = null) =>
            throw new NotSupportedException();

        public void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddAction(
            RumActionType type,
            string name,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StartAction(
            RumActionType type,
            string name,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopAction(
            RumActionType type,
            string? name = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddError(
            Exception exception,
            RumErrorSource source = RumErrorSource.Source,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddError(
            string message,
            RumErrorSource source = RumErrorSource.Source,
            string? stack = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StartResource(
            string key,
            RumHttpMethod method,
            string url,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResource(
            string key,
            int? statusCode = null,
            RumResourceKind kind = RumResourceKind.Native,
            long? size = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResourceWithError(
            string key,
            string message,
            int? statusCode = null,
            string? stack = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void StopResourceWithError(
            string key,
            Exception exception,
            int? statusCode = null,
            IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void AddTiming(string name)
        {
        }

        public void AddFeatureFlagEvaluation(string name, object value)
        {
        }

        public void AddAttribute(string key, object? value)
        {
        }

        public void AddAttributes(IReadOnlyDictionary<string, object?> attributes)
        {
        }

        public void RemoveAttribute(string key)
        {
        }

        public void RemoveAttributes(IEnumerable<string> keys)
        {
        }

        public void ReportAppFullyDisplayed()
        {
        }

        public void StopSession()
        {
        }

        public Task<string?> GetCurrentSessionIdAsync() => Task.FromResult<string?>(null);
    }
}
