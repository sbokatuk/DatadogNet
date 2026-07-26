using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// The liveness contract of a logger created before the SDK could make a live one.
/// </summary>
/// <remarks>
/// <see cref="DeferredDatadogLogger"/> is what stands behind <see cref="Datadog.Logger"/>, the DI
/// singleton and every <c>ILogger</c> category logger when they are created too early. The
/// platform adapters only differ in how they build the live logger, so the contract — drop
/// entries while dead, keep configuration, come alive exactly once — is tested here against a
/// recording fake rather than on a device.
/// </remarks>
public class DeferredLoggerTests
{
    [Fact]
    public void Drops_entries_while_dead_without_throwing()
    {
        var logger = new DeferredDatadogLogger(() => null);

        logger.Log(DatadogLogLevel.Info, "too early");
        logger.Error("also too early", new InvalidOperationException("boom"));

        // No assertion beyond "did not throw" is possible, which is the point: dead means dropped.
    }

    [Fact]
    public void Validates_arguments_even_while_dead()
    {
        var logger = new DeferredDatadogLogger(() => null);

        // The null-message contract must not depend on liveness, or the bug only surfaces in
        // production where the SDK is up.
        Assert.Throws<ArgumentNullException>(() => logger.Log(DatadogLogLevel.Info, null!));
    }

    [Fact]
    public void Comes_alive_when_the_factory_can_deliver_and_replays_configuration_in_order()
    {
        var live = new RecordingLogger();
        IDatadogLogger? available = null;
        var logger = new DeferredDatadogLogger(() => available);

        logger.AddTag("build", "42");
        logger.AddAttribute("tenant", "t1");
        logger.RemoveAttribute("tenant");
        logger.Info("dropped");

        available = live;
        logger.Info("delivered");

        // Configuration lands before the first delivered entry, in the order it was applied.
        string[] expected =
            ["AddTag(build,42)", "AddAttribute(tenant,t1)", "RemoveAttribute(tenant)", "Log(Info,delivered)"];
        Assert.Equal(expected, live.Calls);
    }

    [Fact]
    public void Materialises_exactly_once_and_then_goes_straight_through()
    {
        var live = new RecordingLogger();
        var factoryCalls = 0;
        var logger = new DeferredDatadogLogger(() =>
        {
            factoryCalls++;
            return live;
        });

        logger.Info("first");
        logger.Warn("second");
        logger.AddTag("late", "tag");

        Assert.Equal(1, factoryCalls);
        string[] expected = ["Log(Info,first)", "Log(Warn,second)", "AddTag(late,tag)"];
        Assert.Equal(expected, live.Calls);
    }

    /// <summary>Records every call, so order and arguments can be asserted.</summary>
    private sealed class RecordingLogger : IDatadogLogger
    {
        public List<string> Calls { get; } = [];

        public void Log(
            DatadogLogLevel level,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? attributes = null) =>
            Calls.Add($"Log({level},{message})");

        public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Debug, message, exception, attributes);

        public void Info(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Info, message, exception, attributes);

        public void Notice(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Notice, message, exception, attributes);

        public void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Warn, message, exception, attributes);

        public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Error, message, exception, attributes);

        public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
            Log(DatadogLogLevel.Critical, message, exception, attributes);

        public void AddAttribute(string key, object? value) => Calls.Add($"AddAttribute({key},{value})");

        public void RemoveAttribute(string key) => Calls.Add($"RemoveAttribute({key})");

        public void AddTag(string key, string value) => Calls.Add($"AddTag({key},{value})");

        public void RemoveTagsWithKey(string key) => Calls.Add($"RemoveTagsWithKey({key})");
    }
}
