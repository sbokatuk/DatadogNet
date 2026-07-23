namespace DatadogNet;

/// <summary>A monitor that records nothing.</summary>
internal sealed class NoOpRumMonitor : IRumMonitor
{
    public bool IsEnabled => false;

    public bool Debug { get; set; }

    public IRumViewScope StartView(
        string key,
        string? name = null,
        IReadOnlyDictionary<string, object?>? attributes = null) => new NoOpViewScope(key);

    public void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void AddAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void StartAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void StopAction(RumActionType type, string? name = null, IReadOnlyDictionary<string, object?>? attributes = null)
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

    public void StopSession()
    {
    }

    public Task<string?> GetCurrentSessionIdAsync() => Task.FromResult<string?>(null);

    private sealed class NoOpViewScope(string key) : IRumViewScope
    {
        public string Key { get; } = key;

        public void Stop(IReadOnlyDictionary<string, object?>? attributes = null)
        {
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>Logs that go nowhere.</summary>
internal sealed class NoOpLogs : IDatadogLogs
{
    public bool IsEnabled => false;

    public IDatadogLogger CreateLogger(LoggerOptions? options = null) => new NoOpLogger();

    public void AddAttribute(string key, object? value)
    {
    }

    public void RemoveAttribute(string key)
    {
    }
}

/// <summary>A logger that discards everything.</summary>
internal sealed class NoOpLogger : IDatadogLogger
{
    public void Log(
        DatadogLogLevel level,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Info(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Notice(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Warn(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? attributes = null)
    {
    }

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

/// <summary>A tracer whose spans are never sent.</summary>
internal sealed class NoOpTracer : IDatadogTracer
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders =
        new Dictionary<string, string>();

    public bool IsEnabled => false;

    public IDatadogSpan? ActiveSpan => null;

    public IDatadogSpan StartSpan(
        string operationName,
        IDatadogSpan? parent = null,
        IReadOnlyDictionary<string, object?>? tags = null) => new NoOpSpan();

    public IReadOnlyDictionary<string, string> Inject(IDatadogSpan span) => NoHeaders;

    private sealed class NoOpSpan : IDatadogSpan
    {
        public string TraceId => string.Empty;

        public string SpanId => string.Empty;

        public void SetTag(string key, string value)
        {
        }

        public void SetTag(string key, double value)
        {
        }

        public void SetTag(string key, bool value)
        {
        }

        public void SetError(Exception exception)
        {
        }

        public void SetError(string kind, string message, string? stack = null)
        {
        }

        public void Log(IReadOnlyDictionary<string, object?> fields)
        {
        }

        public IDisposable Activate() => NoOpScope.Instance;

        public void Finish()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>Session Replay with nothing to record.</summary>
internal sealed class NoOpSessionReplay : ISessionReplay
{
    public bool IsEnabled => false;

    public void StartRecording()
    {
    }

    public void StopRecording()
    {
    }
}
