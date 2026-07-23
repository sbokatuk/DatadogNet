namespace DatadogNet;

/// <summary>
/// How often the SDK samples mobile vitals — CPU, memory and refresh rate.
/// </summary>
public enum VitalsUpdateFrequency
{
    /// <summary>Every 100 ms. The most detail, and the most overhead.</summary>
    Frequent,

    /// <summary>Every 500 ms. The SDK's default.</summary>
    Average,

    /// <summary>Every second.</summary>
    Rare,

    /// <summary>Do not collect vitals.</summary>
    Never,
}

/// <summary>
/// Real User Monitoring: views, actions, resources, errors and mobile vitals.
/// </summary>
/// <remarks>
/// Assign to <see cref="DatadogConfiguration.Rum"/> to enable RUM. Everything reported afterwards
/// goes through <see cref="Datadog.Rum"/>.
/// </remarks>
public sealed class RumOptions
{
    /// <summary>
    /// The RUM application id, from <c>UX Monitoring → RUM Applications</c> in Datadog.
    /// </summary>
    /// <remarks>
    /// Distinct from the client token, and distinct per application rather than per organisation.
    /// A single Datadog RUM application can serve both platform heads of one MAUI app.
    /// </remarks>
    public required string ApplicationId { get; init; }

    /// <summary>
    /// Percentage of sessions kept, 0 to 100. Defaults to 100.
    /// </summary>
    /// <remarks>
    /// Sampled per session, not per event, so a kept session is complete rather than full of holes.
    /// 100 while developing; a shipped app usually samples down.
    /// </remarks>
    public float SessionSampleRate { get; init; } = 100;

    /// <summary>
    /// Percentage of the SDK's own telemetry kept, 0 to 100. Defaults to 20, the SDK's own default.
    /// </summary>
    public float TelemetrySampleRate { get; init; } = 20;

    /// <summary>
    /// Detect rage taps, dead clicks and error taps. Defaults to <see langword="true"/>.
    /// </summary>
    public bool TrackFrustrations { get; init; } = true;

    /// <summary>
    /// Report events that happen while no view is active — while the app is backgrounded, in
    /// practice. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Off by default in both native SDKs because it bills sessions that a user never saw. Worth
    /// turning on when you care about background work: a push handler, a sync, a background fetch.
    /// </remarks>
    public bool TrackBackgroundEvents { get; init; }

    /// <summary>
    /// Give an unidentified user a stable anonymous id across sessions. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool TrackAnonymousUser { get; init; }

    /// <summary>How often mobile vitals are sampled.</summary>
    public VitalsUpdateFrequency VitalsUpdateFrequency { get; init; } = VitalsUpdateFrequency.Average;

    /// <summary>
    /// Report main-thread work longer than this as a long task. Defaults to 100 ms, the SDK's own
    /// default on both platforms. <see langword="null"/> disables long-task tracking.
    /// </summary>
    public TimeSpan? LongTaskThreshold { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Let the SDK instrument the platform UI toolkit for views and taps. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// What this buys differs sharply by platform, and neither answer is a substitute for
    /// reporting your own views with <see cref="IRumMonitor.StartView"/>:
    /// <list type="bullet">
    /// <item>
    /// <b>iOS</b> — installs the default UIKit view and action predicates. MAUI renders through
    /// UIKit, so taps are captured with no code of yours, and views are reported per
    /// <c>UIViewController</c>, which for a Shell app is roughly per page.
    /// </item>
    /// <item>
    /// <b>Android</b> — enables user-interaction tracking and the activity view-tracking strategy.
    /// Taps are captured; views are not usefully, because MAUI renders every page into a single
    /// <c>Activity</c> and so reports one view for the whole app. It is still worth having, since
    /// it is what reports app start and ANRs.
    /// </item>
    /// </list>
    /// <c>DatadogNet.Maui</c>'s navigation tracking is the cross-platform answer for views.
    /// </remarks>
    public bool TrackAutomaticInstrumentation { get; init; } = true;

    /// <summary>
    /// Send RUM data somewhere other than the site's intake. For a proxy or a local test.
    /// </summary>
    public Uri? CustomEndpoint { get; init; }

    /// <summary>
    /// Reaches the native RUM configuration before RUM is enabled.
    /// </summary>
    /// <remarks>
    /// The argument is <c>Com.Datadog.Android.Rum.RumConfiguration.Builder</c> on Android and
    /// <c>DatadogObjc.DDRUMConfiguration</c> on iOS.
    /// <para>
    /// This is where RUM's five event mappers live — <c>SetViewEventMapper</c>,
    /// <c>SetActionEventMapper</c>, <c>SetResourceEventMapper</c>, <c>SetErrorEventMapper</c> and
    /// <c>SetLongTaskEventMapper</c>. They are the supported way to redact or drop an event on the
    /// device before it is uploaded, they can only be set here, and they are not exposed
    /// cross-platform because the event models they hand you are large, entirely different between
    /// the two SDKs, and generated from separate schemas. Reaching them through this hook costs one
    /// platform conditional and loses nothing.
    /// </para>
    /// <para>
    /// Also where the platform-specific knobs live: <c>TrackWatchdogTerminations</c>,
    /// <c>AppHangThreshold</c> and the SwiftUI predicates on iOS; <c>TrackNonFatalAnrs</c>,
    /// <c>CollectAccessibility</c>, <c>SetSlowFramesConfiguration</c> and the other view-tracking
    /// strategies on Android. See <c>docs/native-surface.md</c> for the full list of what is
    /// deliberately left here rather than lifted into <see cref="RumOptions"/>.
    /// </para>
    /// </remarks>
    public Action<object>? ConfigureNative { get; init; }
}
