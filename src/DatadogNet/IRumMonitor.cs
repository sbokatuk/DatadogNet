namespace DatadogNet;

/// <summary>The kind of interaction a RUM action records.</summary>
/// <remarks>
/// Only the four types both SDKs declare. dd-sdk-android additionally has <c>CLICK</c> and
/// <c>BACK</c>, which dd-sdk-ios has no counterpart for — a shared member for either would have to
/// silently become something else on iOS, and an action whose type changes by platform cannot be
/// grouped in Datadog.
/// </remarks>
public enum RumActionType
{
    /// <summary>A tap.</summary>
    Tap,

    /// <summary>A scroll. Continuous — use <see cref="IRumMonitor.StartAction"/>.</summary>
    Scroll,

    /// <summary>A swipe. Continuous — use <see cref="IRumMonitor.StartAction"/>.</summary>
    Swipe,

    /// <summary>Anything else.</summary>
    Custom,
}

/// <summary>Where a RUM error came from.</summary>
/// <remarks>
/// Only the five both SDKs declare. dd-sdk-android additionally has <c>AGENT</c>, <c>LOGGER</c> and
/// <c>REPORT</c>, which the SDK sets for itself and an app has no reason to.
/// </remarks>
public enum RumErrorSource
{
    /// <summary>The application's own code. The default.</summary>
    Source,

    /// <summary>A network request.</summary>
    Network,

    /// <summary>Content in a web view.</summary>
    WebView,

    /// <summary>A console message.</summary>
    Console,

    /// <summary>Anything else.</summary>
    Custom,
}

/// <summary>What kind of thing a RUM resource is.</summary>
public enum RumResourceKind
{
    /// <summary>An XMLHttpRequest.</summary>
    Xhr,

    /// <summary>A fetch() call.</summary>
    Fetch,

    /// <summary>
    /// A request made by the platform's own networking stack. The right answer for an
    /// <see cref="System.Net.Http.HttpClient"/> call.
    /// </summary>
    Native,

    /// <summary>A document.</summary>
    Document,

    /// <summary>An image.</summary>
    Image,

    /// <summary>A stylesheet.</summary>
    Css,

    /// <summary>A script.</summary>
    Js,

    /// <summary>A font.</summary>
    Font,

    /// <summary>Audio or video.</summary>
    Media,

    /// <summary>A beacon.</summary>
    Beacon,

    /// <summary>Anything else.</summary>
    Other,
}

/// <summary>The HTTP method of a RUM resource.</summary>
public enum RumHttpMethod
{
    /// <summary>GET.</summary>
    Get,

    /// <summary>POST.</summary>
    Post,

    /// <summary>PUT.</summary>
    Put,

    /// <summary>PATCH.</summary>
    Patch,

    /// <summary>DELETE.</summary>
    Delete,

    /// <summary>HEAD.</summary>
    Head,

    /// <summary>OPTIONS.</summary>
    Options,

    /// <summary>CONNECT.</summary>
    Connect,

    /// <summary>TRACE.</summary>
    Trace,
}

/// <summary>
/// A started RUM view. Disposing it stops the view.
/// </summary>
/// <remarks>
/// The native API on both platforms is a <c>startView</c>/<c>stopView</c> pair matched by key, and
/// a view left open by an early return or an exception is not an error — it goes on collecting
/// every later action and error in the session, attributed to a screen the user has left. Scoping
/// it to a <see langword="using"/> block is the difference between that being possible and not.
/// <para>
/// Stopping is idempotent, so calling <see cref="Stop"/> to attach attributes known only at the end
/// and then leaving the block is safe.
/// </para>
/// </remarks>
public interface IRumViewScope : IDisposable
{
    /// <summary>The key the view was started with.</summary>
    string Key { get; }

    /// <summary>Stops the view, if it is still open.</summary>
    /// <param name="attributes">Attributes attached at stop time.</param>
    void Stop(IReadOnlyDictionary<string, object?>? attributes = null);
}

/// <summary>
/// Reports Real User Monitoring events: views, actions, resources and errors.
/// </summary>
/// <remarks>
/// Reached through <see cref="Datadog.Rum"/>. Every member is safe to call before
/// <see cref="Datadog.Initialize"/> or on a platform with no Datadog support — the call is dropped
/// rather than throwing, because instrumentation that crashes the app it is measuring is worse than
/// instrumentation that is missing.
/// <para>
/// An interface rather than a static class so it can be injected and substituted in tests; the
/// MAUI package registers the live one in the service container.
/// </para>
/// </remarks>
public interface IRumMonitor
{
    /// <summary>Whether RUM was enabled by <see cref="DatadogConfiguration.Rum"/>.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Draw a debug overlay on top of the app showing the current view and session.
    /// </summary>
    /// <remarks>For checking your instrumentation, not for shipping.</remarks>
    bool Debug { get; set; }

    /// <summary>
    /// Starts a RUM view, and returns a scope that stops it when disposed.
    /// </summary>
    /// <param name="key">
    /// Identifies the view. Must be unique among views open at the same time; a stable route or
    /// page identifier is the usual choice.
    /// </param>
    /// <param name="name">The name shown in Datadog. Defaults to <paramref name="key"/>.</param>
    /// <param name="attributes">Attributes attached to the view.</param>
    /// <returns>A scope that stops the view when disposed.</returns>
    /// <remarks>
    /// This is the call a MAUI app needs most, and the one automatic instrumentation cannot do for
    /// it: a MAUI page is not a <c>UIViewController</c> or an <c>Activity</c>, so neither SDK's
    /// view tracking sees your screens. <c>DatadogNet.Maui</c>'s navigation tracking calls this for
    /// you; do it by hand where you want a view that is not a page.
    /// <code>
    /// using (Datadog.Rum.StartView ("checkout", "Checkout")) {
    ///     // every action and error in here belongs to the checkout view
    /// }
    /// </code>
    /// </remarks>
    IRumViewScope StartView(
        string key,
        string? name = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Stops a view started with the same key.</summary>
    void StopView(string key, IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Records an instantaneous action — a tap, typically.</summary>
    void AddAction(
        RumActionType type,
        string name,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Begins a continuous action — a scroll or a swipe.
    /// </summary>
    /// <remarks>Ended by <see cref="StopAction"/>, or by the SDK when the view stops.</remarks>
    void StartAction(
        RumActionType type,
        string name,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Ends a continuous action begun by <see cref="StartAction"/>.</summary>
    void StopAction(
        RumActionType type,
        string? name = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Records a .NET exception as a RUM error.
    /// </summary>
    /// <remarks>
    /// The exception's type, message and stack trace reach Datadog as the error's kind, message and
    /// stack, so errors group by where they were thrown rather than by message text. Neither native
    /// SDK can take a managed exception directly — one wants a <c>java.lang.Throwable</c> and the
    /// other an <c>NSError</c> — which is why this overload exists at all.
    /// </remarks>
    void AddError(
        Exception exception,
        RumErrorSource source = RumErrorSource.Source,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Records an error that is not an exception.</summary>
    void AddError(
        string message,
        RumErrorSource source = RumErrorSource.Source,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Begins tracking a network request as a RUM resource.
    /// </summary>
    /// <param name="key">Identifies the request until it is stopped. Unique while in flight.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The absolute URL.</param>
    /// <param name="attributes">Attributes attached to the resource.</param>
    /// <remarks>
    /// <see cref="DatadogHttpMessageHandler"/> does this for every request that passes through it,
    /// which is the way to get an <see cref="System.Net.Http.HttpClient"/>'s traffic into RUM.
    /// Neither SDK's automatic network instrumentation sees it: on Android that hooks OkHttp, which
    /// <c>HttpClient</c> does not route through by default, and on iOS it hooks
    /// <c>NSURLSession</c> delegates, which <c>NSUrlSessionHandler</c> owns rather than exposes.
    /// </remarks>
    void StartResource(
        string key,
        RumHttpMethod method,
        string url,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Completes a resource begun by <see cref="StartResource"/>.</summary>
    /// <param name="key">The key the resource was started with.</param>
    /// <param name="statusCode">The HTTP status code, if there was one.</param>
    /// <param name="kind">What kind of resource it was.</param>
    /// <param name="size">The response body size in bytes, if known.</param>
    /// <param name="attributes">Attributes attached to the resource.</param>
    void StopResource(
        string key,
        int? statusCode = null,
        RumResourceKind kind = RumResourceKind.Native,
        long? size = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Completes a resource that failed.</summary>
    void StopResourceWithError(
        string key,
        string message,
        int? statusCode = null,
        string? stack = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Completes a resource that failed, from an exception.</summary>
    void StopResourceWithError(
        string key,
        Exception exception,
        int? statusCode = null,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Marks a named moment within the current view, timed from when the view started.
    /// </summary>
    /// <remarks>
    /// The way to answer "how long until this screen was actually usable" — first paint, data
    /// loaded, list rendered. Reported as <c>@view.custom_timings.&lt;name&gt;</c>.
    /// </remarks>
    void AddTiming(string name);

    /// <summary>
    /// Records that a feature flag was evaluated, so RUM events can be split by variant.
    /// </summary>
    /// <param name="name">The flag's name.</param>
    /// <param name="value">The value it evaluated to.</param>
    void AddFeatureFlagEvaluation(string name, object value);

    /// <summary>Adds an attribute to every subsequent RUM event.</summary>
    void AddAttribute(string key, object? value);

    /// <summary>Adds several attributes to every subsequent RUM event.</summary>
    void AddAttributes(IReadOnlyDictionary<string, object?> attributes);

    /// <summary>Removes a global attribute added by <see cref="AddAttribute"/>.</summary>
    void RemoveAttribute(string key);

    /// <summary>Removes several global attributes.</summary>
    void RemoveAttributes(IEnumerable<string> keys);

    /// <summary>
    /// Ends the current session. The next event starts a new one.
    /// </summary>
    /// <remarks>Sign-out is the usual reason.</remarks>
    void StopSession();

    /// <summary>
    /// The id of the current RUM session, or <see langword="null"/> if there is none — because RUM
    /// is not enabled, or because this session was dropped by sampling.
    /// </summary>
    /// <remarks>
    /// Worth attaching to a support ticket: it is what turns "the app was slow" into a session you
    /// can watch. Asynchronous because both SDKs answer through a callback on their own queue.
    /// </remarks>
    Task<string?> GetCurrentSessionIdAsync();
}
