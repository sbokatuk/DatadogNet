using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// What the <c>ILogger</c> bridge actually delivers to Datadog: structure, not just text.
/// </summary>
/// <remarks>
/// Observed entirely through the public surface: a fake <see cref="IDatadogLogs"/> registered in
/// the host is what the logging provider builds its loggers from, so these tests watch real
/// <c>Microsoft.Extensions.Logging</c> plumbing — factory, scope provider, category creation —
/// deliver into a recording sink.
/// </remarks>
public class LoggerBridgeTests
{
    [Fact]
    public void Template_values_become_attributes_and_the_template_groups_them()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            logger.LogInformation("Order {OrderId} placed by {Customer}", 42, "jo");

            var entry = Assert.Single(sink.Entries);
            Assert.Equal(DatadogLogLevel.Info, entry.Level);
            Assert.Equal("Order 42 placed by jo", entry.Message);
            Assert.NotNull(entry.Attributes);
            Assert.Equal(42, entry.Attributes!["OrderId"]);
            Assert.Equal("jo", entry.Attributes["Customer"]);
            Assert.Equal("Order {OrderId} placed by {Customer}", entry.Attributes["logger.template"]);
        }
    }

    [Fact]
    public void A_message_without_placeholders_carries_no_template_attribute()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            logger.LogInformation("plain text");

            var entry = Assert.Single(sink.Entries);
            // The template equals the message, so it groups nothing and earns no attribute; with
            // no other structure the whole attributes dictionary stays null and the native call
            // is exactly what it was before the bridge learned structure.
            Assert.Null(entry.Attributes);
        }
    }

    [Fact]
    public void Structured_scope_values_become_attributes()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            using (logger.BeginScope(new Dictionary<string, object?> { ["TenantId"] = "t1" }))
            using (logger.BeginScope("processing batch 7"))
            {
                logger.LogInformation("inside");
            }

            var entry = Assert.Single(sink.Entries);
            Assert.NotNull(entry.Attributes);
            Assert.Equal("t1", entry.Attributes!["TenantId"]);
            var plain = Assert.IsType<List<object?>>(entry.Attributes["logger.scope"]);
            Assert.Equal("processing batch 7", Assert.Single(plain));
        }
    }

    [Fact]
    public void The_entry_shadows_its_scopes_and_inner_scopes_shadow_outer()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            using (logger.BeginScope(new Dictionary<string, object?> { ["Stage"] = "outer", ["Region"] = "eu" }))
            using (logger.BeginScope(new Dictionary<string, object?> { ["Stage"] = "inner" }))
            {
                logger.LogInformation("Stage {Stage}", "entry");
            }

            var entry = Assert.Single(sink.Entries);
            Assert.NotNull(entry.Attributes);
            // The call site is more specific than its surroundings, and inner beats outer —
            // the shadowing every structured sink applies.
            Assert.Equal("entry", entry.Attributes!["Stage"]);
            Assert.Equal("eu", entry.Attributes["Region"]);
        }
    }

    [Fact]
    public void Event_ids_are_still_captured()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            logger.Log(LogLevel.Warning, new EventId(7, "SlowPath"), "careful");

            var entry = Assert.Single(sink.Entries);
            Assert.NotNull(entry.Attributes);
            Assert.Equal(7, entry.Attributes!["logger.event_id"]);
            Assert.Equal("SlowPath", entry.Attributes["logger.event_name"]);
        }
    }

    [Fact]
    public void The_category_becomes_the_logger_name()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            provider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MyApp.Services.OrderService")
                .LogInformation("hello");

            Assert.Equal("MyApp.Services.OrderService", Assert.Single(sink.CreatedLoggerNames));
        }
    }

    [Fact]
    public void The_minimum_level_still_filters()
    {
        var (provider, sink) = BuildHost();
        using (provider)
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("MyApp.Orders");

            logger.LogDebug("below the default Information floor");

            Assert.Empty(sink.Entries);
        }
    }

    /// <summary>A host whose Datadog sink is a recording fake.</summary>
    private static (ServiceProvider Provider, RecordingLogs Sink) BuildHost()
    {
        var sink = new RecordingLogs();
        var services = new ServiceCollection();
        services.AddSingleton<IDatadogLogs>(sink);
        services.AddLogging(logging => logging.AddDatadog());

        return (services.BuildServiceProvider(), sink);
    }

    private sealed record LogEntry(
        DatadogLogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?>? Attributes);

    private sealed class RecordingLogs : IDatadogLogs
    {
        public List<LogEntry> Entries { get; } = [];

        public List<string?> CreatedLoggerNames { get; } = [];

        public bool IsEnabled => true;

        public IDatadogLogger CreateLogger(LoggerOptions? options = null)
        {
            CreatedLoggerNames.Add(options?.Name);
            return new RecordingLogger(Entries);
        }

        public void AddAttribute(string key, object? value)
        {
        }

        public void RemoveAttribute(string key)
        {
        }
    }

    private sealed class RecordingLogger(List<LogEntry> entries) : IDatadogLogger
    {
        public void Log(
            DatadogLogLevel level,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? attributes = null) =>
            entries.Add(new LogEntry(level, message, exception, attributes));

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

        public void AddAttribute(string key, object? value)
        {
        }

        public void RemoveAttribute(string key)
        {
        }

        public void AddTag(string key, string value)
        {
        }

        public void RemoveTagsWithKey(string key)
        {
        }
    }
}
