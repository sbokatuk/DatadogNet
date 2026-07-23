using DatadogObjc;
using Foundation;

namespace DatadogNet;

/// <summary>RUM over <c>DDRUMMonitor</c>.</summary>
/// <remarks>
/// <c>DDRUMMonitor.Shared</c> is resolved per call rather than cached. The shared monitor is a
/// no-op instance until RUM is enabled and the real one afterwards, so caching it in a field the
/// first time <see cref="Datadog.Rum"/> is touched would permanently bind whatever existed at that
/// moment — and this façade deliberately lets you reach <see cref="Datadog.Rum"/> before
/// <see cref="Datadog.Initialize"/>.
/// </remarks>
internal sealed class IosRumMonitor : IRumMonitor
{
    private static DDRUMMonitor Monitor => DDRUMMonitor.Shared;

    public bool IsEnabled => Datadog.Configuration?.Rum is not null;

    public bool Debug
    {
        get => Monitor.Debug;
        set => Monitor.Debug = value;
    }

    public IRumViewScope StartView(
        string key,
        string? name = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        Monitor.StartViewWithKey(key, name ?? key, DatadogAttributes.From(attributes));
        return new ViewScope(key);
    }

    public void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        Monitor.StopViewWithKey(key, DatadogAttributes.From(attributes));
    }

    public void AddAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.AddActionWithType(ToNative(type), name, DatadogAttributes.From(attributes));

    public void StartAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StartActionWithType(ToNative(type), name, DatadogAttributes.From(attributes));

    public void StopAction(RumActionType type, string? name = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StopActionWithType(ToNative(type), name, DatadogAttributes.From(attributes));

    public void AddError(
        Exception exception,
        RumErrorSource source = RumErrorSource.Source,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // AddErrorWithMessage rather than AddErrorWithError: the latter takes an NSError, which a
        // managed exception is not. Passing the stack separately is what makes Datadog group these
        // by where they were thrown rather than by message text.
        Monitor.AddErrorWithMessage(
            $"{exception.GetType().FullName}: {exception.Message}",
            exception.StackTrace,
            ToNative(source),
            DatadogAttributes.From(attributes));
    }

    public void AddError(
        string message,
        RumErrorSource source = RumErrorSource.Source,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        Monitor.AddErrorWithMessage(message, stack, ToNative(source), DatadogAttributes.From(attributes));
    }

    public void StartResource(
        string key,
        RumHttpMethod method,
        string url,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StartResourceWithResourceKey(key, ToNative(method), url, DatadogAttributes.From(attributes));

    public void StopResource(
        string key,
        int? statusCode = null,
        RumResourceKind kind = RumResourceKind.Native,
        long? size = null,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StopResourceWithResourceKey(
            key,
            statusCode is { } code ? NSNumber.FromInt32(code) : null,
            ToNative(kind),
            size is { } bytes ? NSNumber.FromInt64(bytes) : null,
            DatadogAttributes.From(attributes));

    public void StopResourceWithError(
        string key,
        string message,
        int? statusCode = null,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        // The bound overloads take an NSURLResponse to carry the status code, and there is no
        // overload that takes a bare status code - so a status code with no response object has
        // nowhere to go but the attributes. Under Datadog's own reserved name, so it still renders
        // as the resource's status rather than as a stray custom attribute.
        var payload = WithStatusCode(attributes, statusCode);

        Monitor.StopResourceWithErrorWithResourceKey(key, message, response: null, DatadogAttributes.From(payload));
        _ = stack;
    }

    public void StopResourceWithError(
        string key,
        Exception exception,
        int? statusCode = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        StopResourceWithError(
            key,
            $"{exception.GetType().FullName}: {exception.Message}",
            statusCode,
            exception.StackTrace,
            attributes);
    }

    public void AddTiming(string name) => Monitor.AddTimingWithName(name);

    public void AddFeatureFlagEvaluation(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Monitor.AddFeatureFlagEvaluationWithName(name, NativeAttributes.Single(name, value));
    }

    public void AddAttribute(string key, object? value) =>
        Monitor.AddAttributeForKey(key, NativeAttributes.Single(key, value));

    public void AddAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        Monitor.AddAttributes(DatadogAttributes.From(attributes));
    }

    public void RemoveAttribute(string key) => Monitor.RemoveAttributeForKey(key);

    public void RemoveAttributes(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        Monitor.RemoveAttributesForKeys([.. keys]);
    }

    public void StopSession() => Monitor.StopSession();

    public Task<string?> GetCurrentSessionIdAsync()
    {
        // TaskCreationOptions.RunContinuationsAsynchronously: the completion runs on whichever
        // queue the SDK answers on, and a synchronous continuation would run the caller's
        // await-resumption there too.
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Monitor.CurrentSessionIDWithCompletion(sessionId => completion.TrySetResult(sessionId?.ToString()));

        return completion.Task;
    }

    private static IReadOnlyDictionary<string, object?>? WithStatusCode(
        IReadOnlyDictionary<string, object?>? attributes,
        int? statusCode)
    {
        if (statusCode is not { } code)
        {
            return attributes;
        }

        var merged = new Dictionary<string, object?>();
        if (attributes is not null)
        {
            foreach (var pair in attributes)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        merged["resource.status_code"] = code;
        return merged;
    }

    private static DDRUMActionType ToNative(RumActionType type) => type switch
    {
        RumActionType.Scroll => DDRUMActionType.Scroll,
        RumActionType.Swipe => DDRUMActionType.Swipe,
        RumActionType.Custom => DDRUMActionType.Custom,
        _ => DDRUMActionType.Tap,
    };

    private static DDRUMErrorSource ToNative(RumErrorSource source) => source switch
    {
        RumErrorSource.Network => DDRUMErrorSource.Network,
        RumErrorSource.WebView => DDRUMErrorSource.Webview,
        RumErrorSource.Console => DDRUMErrorSource.Console,
        RumErrorSource.Custom => DDRUMErrorSource.Custom,
        _ => DDRUMErrorSource.Source,
    };

    private static DDRUMMethod ToNative(RumHttpMethod method) => method switch
    {
        RumHttpMethod.Post => DDRUMMethod.Post,
        RumHttpMethod.Put => DDRUMMethod.Put,
        RumHttpMethod.Patch => DDRUMMethod.Patch,
        RumHttpMethod.Delete => DDRUMMethod.Delete,
        RumHttpMethod.Head => DDRUMMethod.Head,
        RumHttpMethod.Options => DDRUMMethod.Options,
        RumHttpMethod.Connect => DDRUMMethod.Connect,
        RumHttpMethod.Trace => DDRUMMethod.Trace,
        _ => DDRUMMethod.Get,
    };

    private static DDRUMResourceType ToNative(RumResourceKind kind) => kind switch
    {
        RumResourceKind.Xhr => DDRUMResourceType.Xhr,
        RumResourceKind.Fetch => DDRUMResourceType.Fetch,
        RumResourceKind.Document => DDRUMResourceType.Document,
        RumResourceKind.Image => DDRUMResourceType.Image,
        RumResourceKind.Css => DDRUMResourceType.Css,
        RumResourceKind.Js => DDRUMResourceType.Js,
        RumResourceKind.Font => DDRUMResourceType.Font,
        RumResourceKind.Media => DDRUMResourceType.Media,
        RumResourceKind.Beacon => DDRUMResourceType.Beacon,
        RumResourceKind.Other => DDRUMResourceType.Other,
        _ => DDRUMResourceType.Native,
    };

    private sealed class ViewScope(string key) : IRumViewScope
    {
        private bool stopped;

        public string Key { get; } = key;

        public void Stop(IReadOnlyDictionary<string, object?>? attributes = null)
        {
            if (stopped)
            {
                return;
            }

            stopped = true;
            DDRUMMonitor.Shared.StopViewWithKey(Key, DatadogAttributes.From(attributes));
        }

        public void Dispose() => Stop();
    }
}
