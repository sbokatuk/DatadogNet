namespace DatadogNet;

/// <summary>
/// A logger created before the SDK could make a live one, which comes alive on its own once it can.
/// </summary>
/// <remarks>
/// Both native SDKs answer "create a logger" with a permanently dead no-op when Logs is not enabled
/// yet — and loggers are exactly the surface consumers create early and cache forever:
/// <see cref="Datadog.Logger"/>, the <c>IDatadogLogger</c> singleton the dependency-injection
/// package registers, one <c>ILogger</c> per category. Binding the native logger eagerly would
/// freeze that dead no-op in, and every entry for the life of the app would be dropped even after
/// <see cref="Datadog.Initialize"/>. RUM and Trace dodge this by resolving their native per call;
/// a logger cannot, because it carries per-logger state (name, attributes, tags) the native holds.
/// <para>
/// So: hold the recipe instead of the corpse. <paramref name="materialize"/> returns the live
/// logger once the SDK can actually make one, and <see langword="null"/> until then. Entries
/// written while dead are dropped — the same documented contract as every other pre-Initialize
/// call — but configuration (<see cref="AddAttribute"/>, <see cref="AddTag"/> and their removals)
/// is queued and replayed in order at materialisation, because "the tag you set during startup
/// silently never applied" is a config loss, not a dropped event. Once live, the logger freezes:
/// all further calls go straight to the native with no lock on the write path.
/// </para>
/// </remarks>
internal sealed class DeferredDatadogLogger(Func<IDatadogLogger?> materialize) : IDatadogLogger
{
    private readonly object gate = new();

    /// <summary>Configuration applied while dead, replayed in order when the logger comes alive.</summary>
    private List<Action<IDatadogLogger>>? pendingConfiguration;

    private volatile IDatadogLogger? live;

    /// <summary>The live logger, materialising it if that has just become possible.</summary>
    private IDatadogLogger? Live
    {
        get
        {
            if (live is { } alreadyAlive)
            {
                return alreadyAlive;
            }

            lock (gate)
            {
                if (live is null && materialize() is { } created)
                {
                    if (pendingConfiguration is { } pending)
                    {
                        foreach (var apply in pending)
                        {
                            apply(created);
                        }

                        pendingConfiguration = null;
                    }

                    // Assigned only after the queued configuration is in, so a concurrent writer
                    // that sees the fast path cannot log ahead of the attributes it relies on.
                    live = created;
                }

                return live;
            }
        }
    }

    public void Log(
        DatadogLogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        // Argument validation stays constant across liveness: a null message throws whether the
        // entry would have been sent or dropped, so the bug surfaces in development too.
        ArgumentNullException.ThrowIfNull(message);
        Live?.Log(level, message, exception, attributes);
    }

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

    public void AddAttribute(string key, object? value) => Configure(logger => logger.AddAttribute(key, value));

    public void RemoveAttribute(string key) => Configure(logger => logger.RemoveAttribute(key));

    public void AddTag(string key, string value) => Configure(logger => logger.AddTag(key, value));

    public void RemoveTagsWithKey(string key) => Configure(logger => logger.RemoveTagsWithKey(key));

    private void Configure(Action<IDatadogLogger> apply)
    {
        if (Live is { } alive)
        {
            apply(alive);
            return;
        }

        lock (gate)
        {
            // Materialisation may have won the race between the check above and this lock; the
            // queue must not gain entries after it has been replayed and discarded.
            if (live is { } nowAlive)
            {
                apply(nowAlive);
                return;
            }

            (pendingConfiguration ??= []).Add(apply);
        }
    }
}
