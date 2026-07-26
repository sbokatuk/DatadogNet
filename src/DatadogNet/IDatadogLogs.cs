namespace DatadogNet;

/// <summary>
/// Creates loggers, and holds attributes shared by all of them.
/// </summary>
/// <remarks>Reached through <see cref="Datadog.Logs"/>.</remarks>
public interface IDatadogLogs
{
    /// <summary>Whether Logs was enabled by <see cref="DatadogConfiguration.Logs"/>.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates a logger.</summary>
    /// <param name="options">
    /// How it behaves. <see langword="null"/> gives the SDK's own defaults, which correlate with
    /// RUM and traces and upload every level.
    /// </param>
    IDatadogLogger CreateLogger(LoggerOptions? options = null);

    /// <summary>Adds an attribute to every log written by every logger.</summary>
    void AddAttribute(string key, object? value);

    /// <summary>Removes an attribute added by <see cref="AddAttribute"/>.</summary>
    void RemoveAttribute(string key);
}

/// <summary>
/// Writes log entries to Datadog.
/// </summary>
/// <remarks>
/// Created by <see cref="IDatadogLogs.CreateLogger"/>. <see cref="Datadog.Logger"/> is a
/// ready-made one for apps that only want the single obvious logger.
/// <para>
/// Every member is safe to call before <see cref="Datadog.Initialize"/> or on a platform with no
/// Datadog support; the entry is dropped rather than throwing. A logger created before
/// <see cref="Datadog.Initialize"/> is not dead, though: it starts delivering at the first write
/// after the SDK is up, and attributes and tags applied while it waited are kept and applied then
/// — so a logger built during startup, or registered as a DI singleton before the host runs,
/// loses only the entries written too early, not its configuration or its future.
/// </para>
/// </remarks>
public interface IDatadogLogger
{
    /// <summary>
    /// Writes a log entry.
    /// </summary>
    /// <param name="level">Its severity.</param>
    /// <param name="message">What happened.</param>
    /// <param name="exception">
    /// An exception to attach. Its type, message and stack reach Datadog as <c>error.kind</c>,
    /// <c>error.message</c> and <c>error.stack</c>, so the entry renders as an error rather than as
    /// three unrelated custom attributes.
    /// </param>
    /// <param name="attributes">Attributes attached to this entry only.</param>
    /// <remarks>
    /// The level is an argument rather than one method per level, so a level chosen at runtime —
    /// which is what an <c>ILogger</c> bridge has — does not need a switch over six method names.
    /// The per-level shorthands below are for the ordinary case.
    /// </remarks>
    void Log(
        DatadogLogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Debug"/>.</summary>
    void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Info"/>.</summary>
    void Info(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Notice"/>.</summary>
    void Notice(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Warn"/>.</summary>
    void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Error"/>.</summary>
    void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Writes at <see cref="DatadogLogLevel.Critical"/>.</summary>
    void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Adds an attribute to every subsequent entry from this logger.</summary>
    void AddAttribute(string key, object? value);

    /// <summary>Removes an attribute added by <see cref="AddAttribute"/>.</summary>
    void RemoveAttribute(string key);

    /// <summary>Adds a <c>key:value</c> tag to every subsequent entry from this logger.</summary>
    /// <remarks>
    /// Tags are indexed differently from attributes in Datadog: low-cardinality facets you filter
    /// by, rather than per-event detail. A build number is a tag; an order id is an attribute.
    /// </remarks>
    void AddTag(string key, string value);

    /// <summary>Removes every tag with the given key.</summary>
    void RemoveTagsWithKey(string key);
}
