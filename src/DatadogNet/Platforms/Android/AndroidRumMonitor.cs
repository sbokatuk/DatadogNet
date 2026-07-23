using Com.Datadog.Android;
using Com.Datadog.Android.Rum;

using NativeRumMonitor = Com.Datadog.Android.Rum.IRumMonitor;

namespace DatadogNet;

/// <summary>RUM over <c>GlobalRumMonitor</c>.</summary>
/// <remarks>
/// The monitor is resolved per call rather than cached. <c>GlobalRumMonitor.Get()</c> returns a
/// no-op instance until RUM is enabled and the real one afterwards, so caching it the first time
/// <see cref="Datadog.Rum"/> is touched would permanently bind whatever existed at that moment —
/// and this façade deliberately lets you reach <see cref="Datadog.Rum"/> before
/// <see cref="Datadog.Initialize"/>.
/// </remarks>
internal sealed class AndroidRumMonitor : IRumMonitor
{
    private static NativeRumMonitor Monitor => GlobalRumMonitor.Get();

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

        // The key is declared as Object in Kotlin - the SDK keys views by identity so an Activity
        // or Fragment can be passed directly - so a string has to be boxed into a Java.Lang.String.
        Monitor.StartView(new Java.Lang.String(key), name ?? key, DatadogAttributes.From(attributes));

        return new ViewScope(key);
    }

    public void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        Monitor.StopView(new Java.Lang.String(key), DatadogAttributes.From(attributes));
    }

    public void AddAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.AddAction(ToNative(type), name, DatadogAttributes.From(attributes));

    public void StartAction(RumActionType type, string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StartAction(ToNative(type), name, DatadogAttributes.From(attributes));

    public void StopAction(RumActionType type, string? name = null, IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StopAction(ToNative(type), name!, DatadogAttributes.From(attributes));

    public void AddError(
        Exception exception,
        RumErrorSource source = RumErrorSource.Source,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // AddErrorWithStacktrace rather than AddError: the latter takes a java.lang.Throwable,
        // which a managed exception is not. Passing the managed type, message and stack as strings
        // is what gets all three to Datadog without anything crossing the Java exception boundary.
        Monitor.AddErrorWithStacktrace(
            $"{exception.GetType().FullName}: {exception.Message}",
            ToNative(source),
            exception.ToString(),
            DatadogAttributes.From(attributes));
    }

    public void AddError(
        string message,
        RumErrorSource source = RumErrorSource.Source,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        Monitor.AddErrorWithStacktrace(message, ToNative(source), stack!, DatadogAttributes.From(attributes));
    }

    public void StartResource(
        string key,
        RumHttpMethod method,
        string url,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StartResource(key, ToNative(method), url, DatadogAttributes.From(attributes));

    public void StopResource(
        string key,
        int? statusCode = null,
        RumResourceKind kind = RumResourceKind.Native,
        long? size = null,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StopResource(
            key,
            statusCode is { } code ? Java.Lang.Integer.ValueOf(code)! : null,
            size is { } bytes ? Java.Lang.Long.ValueOf(bytes)! : null,
            ToNative(kind),
            DatadogAttributes.From(attributes));

    public void StopResourceWithError(
        string key,
        string message,
        int? statusCode = null,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        Monitor.StopResourceWithError(
            key,
            statusCode is { } code ? Java.Lang.Integer.ValueOf(code)! : null,
            message,
            Com.Datadog.Android.Rum.RumErrorSource.Network!,
            stackTrace: stack!,
            errorType: null!,
            DatadogAttributes.From(attributes));

    public void StopResourceWithError(
        string key,
        Exception exception,
        int? statusCode = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Monitor.StopResourceWithError(
            key,
            statusCode is { } code ? Java.Lang.Integer.ValueOf(code)! : null,
            exception.Message,
            Com.Datadog.Android.Rum.RumErrorSource.Network!,
            stackTrace: exception.ToString(),
            errorType: exception.GetType().FullName!,
            DatadogAttributes.From(attributes));
    }

    public void AddTiming(string name) => Monitor.AddTiming(name);

    public void AddFeatureFlagEvaluation(string name, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Monitor.AddFeatureFlagEvaluation(name, NativeAttributes.Single(name, value));
    }

    public void AddAttribute(string key, object? value) =>
        Monitor.AddAttribute(key, NativeAttributes.Single(key, value));

    public void AddAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        // dd-sdk-android has no bulk setter - dd-sdk-ios added addAttributes for the cost of
        // crossing the bridge per key, which is not a cost that exists here.
        foreach (var pair in attributes)
        {
            Monitor.AddAttribute(pair.Key, NativeAttributes.Single(pair.Key, pair.Value));
        }
    }

    public void RemoveAttribute(string key) => Monitor.RemoveAttribute(key);

    public void RemoveAttributes(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        foreach (var key in keys)
        {
            Monitor.RemoveAttribute(key);
        }
    }

    public void StopSession() => Monitor.StopSession();

    public Task<string?> GetCurrentSessionIdAsync()
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Monitor.GetCurrentSessionId(new SessionIdCallback(completion));

        return completion.Task;
    }

    private static Com.Datadog.Android.Rum.RumActionType ToNative(RumActionType type) => type switch
    {
        RumActionType.Scroll => Com.Datadog.Android.Rum.RumActionType.Scroll!,
        RumActionType.Swipe => Com.Datadog.Android.Rum.RumActionType.Swipe!,
        RumActionType.Custom => Com.Datadog.Android.Rum.RumActionType.Custom!,
        _ => Com.Datadog.Android.Rum.RumActionType.Tap!,
    };

    private static Com.Datadog.Android.Rum.RumErrorSource ToNative(DatadogNet.RumErrorSource source) => source switch
    {
        DatadogNet.RumErrorSource.Network => Com.Datadog.Android.Rum.RumErrorSource.Network!,
        DatadogNet.RumErrorSource.WebView => Com.Datadog.Android.Rum.RumErrorSource.Webview!,
        DatadogNet.RumErrorSource.Console => Com.Datadog.Android.Rum.RumErrorSource.Console!,
        DatadogNet.RumErrorSource.Custom => Com.Datadog.Android.Rum.RumErrorSource.Custom!,
        _ => Com.Datadog.Android.Rum.RumErrorSource.Source!,
    };

    private static RumResourceMethod ToNative(RumHttpMethod method) => method switch
    {
        RumHttpMethod.Post => RumResourceMethod.Post!,
        RumHttpMethod.Put => RumResourceMethod.Put!,
        RumHttpMethod.Patch => RumResourceMethod.Patch!,
        RumHttpMethod.Delete => RumResourceMethod.Delete!,
        RumHttpMethod.Head => RumResourceMethod.Head!,
        RumHttpMethod.Options => RumResourceMethod.Options!,
        RumHttpMethod.Connect => RumResourceMethod.Connect!,
        RumHttpMethod.Trace => RumResourceMethod.Trace!,
        _ => RumResourceMethod.Get!,
    };

    // Return type fully qualified: unqualified 'RumResourceKind' binds to this façade's own enum,
    // since the file's namespace wins over the using directive.

    private static Com.Datadog.Android.Rum.RumResourceKind ToNative(DatadogNet.RumResourceKind kind) => kind switch
    {
        DatadogNet.RumResourceKind.Xhr => Com.Datadog.Android.Rum.RumResourceKind.Xhr!,
        DatadogNet.RumResourceKind.Fetch => Com.Datadog.Android.Rum.RumResourceKind.Fetch!,
        DatadogNet.RumResourceKind.Document => Com.Datadog.Android.Rum.RumResourceKind.Document!,
        DatadogNet.RumResourceKind.Image => Com.Datadog.Android.Rum.RumResourceKind.Image!,
        DatadogNet.RumResourceKind.Css => Com.Datadog.Android.Rum.RumResourceKind.Css!,
        DatadogNet.RumResourceKind.Js => Com.Datadog.Android.Rum.RumResourceKind.Js!,
        DatadogNet.RumResourceKind.Font => Com.Datadog.Android.Rum.RumResourceKind.Font!,
        DatadogNet.RumResourceKind.Media => Com.Datadog.Android.Rum.RumResourceKind.Media!,
        DatadogNet.RumResourceKind.Beacon => Com.Datadog.Android.Rum.RumResourceKind.Beacon!,
        DatadogNet.RumResourceKind.Other => Com.Datadog.Android.Rum.RumResourceKind.Other!,
        _ => Com.Datadog.Android.Rum.RumResourceKind.Native!,
    };

    /// <summary>
    /// Adapts <c>getCurrentSessionId</c>'s Kotlin lambda to a completion source.
    /// </summary>
    /// <remarks>
    /// The parameter is a <c>kotlin.jvm.functions.Function1</c>, which C# cannot express as a
    /// lambda: it binds as an interface, so a real Java-callable object is required. iOS takes an
    /// ordinary block and needs none of this.
    /// </remarks>
    private sealed class SessionIdCallback(TaskCompletionSource<string?> completion)
        : Java.Lang.Object, Kotlin.Jvm.Functions.IFunction1
    {
        public Java.Lang.Object? Invoke(Java.Lang.Object? sessionId)
        {
            completion.TrySetResult(sessionId?.ToString());

            // Kotlin's Unit. Returning null is what the binding maps a Unit-returning lambda to.
            return null;
        }
    }

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
            GlobalRumMonitor.Get().StopView(new Java.Lang.String(Key), DatadogAttributes.From(attributes));
        }

        public void Dispose() => Stop();
    }
}
