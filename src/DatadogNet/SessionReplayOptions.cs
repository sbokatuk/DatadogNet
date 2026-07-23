namespace DatadogNet;

/// <summary>
/// What Session Replay does with text and user input.
/// </summary>
/// <remarks>
/// Masking happens <b>on the device</b>, before anything is uploaded. Loosen it deliberately.
/// </remarks>
public enum TextAndInputPrivacy
{
    /// <summary>Mask every text and every input. The safest, and the default here.</summary>
    MaskAll,

    /// <summary>Show static text; mask everything the user typed.</summary>
    MaskAllInputs,

    /// <summary>
    /// Show static text and ordinary inputs; mask only inputs the platform marks sensitive —
    /// password fields, and the like.
    /// </summary>
    MaskSensitiveInputs,
}

/// <summary>What Session Replay does with images.</summary>
public enum ImagePrivacy
{
    /// <summary>Mask every image. The safest, and the default here.</summary>
    MaskAll,

    /// <summary>
    /// Mask images likely to be content, keep the ones likely to be chrome.
    /// </summary>
    /// <remarks>
    /// The two SDKs decide "likely to be content" differently, and this is the one Session Replay
    /// setting whose behaviour is not identical across platforms:
    /// <list type="bullet">
    /// <item><b>iOS</b> masks images that are not bundled with the app (<c>maskNonBundledOnly</c>) —
    /// so your asset catalogue shows and anything downloaded is masked.</item>
    /// <item><b>Android</b> masks images larger than roughly 100×100 dp (<c>MASK_LARGE_ONLY</c>) —
    /// so icons show and photographs are masked.</item>
    /// </list>
    /// Both are aiming at "hide user content, keep the interface legible", and in a typical app the
    /// visible result is much the same. If exactly which images are masked matters to you, use
    /// <see cref="MaskAll"/>.
    /// </remarks>
    MaskContentImages,

    /// <summary>Mask nothing.</summary>
    MaskNone,
}

/// <summary>What Session Replay does with touch indicators.</summary>
public enum TouchPrivacy
{
    /// <summary>Do not draw touches. The default here.</summary>
    Hide,

    /// <summary>Draw where the user touched.</summary>
    Show,
}

/// <summary>
/// Session Replay: a reconstruction of what the user actually saw.
/// </summary>
/// <remarks>
/// Assign to <see cref="DatadogConfiguration.SessionReplay"/> to enable it. Requires
/// <see cref="DatadogConfiguration.Rum"/> — a replay is attached to a RUM session, and enabling it
/// without RUM records nothing at all.
/// <para>
/// The three privacy levels are required by the native iOS initializer rather than defaulted,
/// specifically so the choice is never made implicitly. They are defaulted here, but to the most
/// private setting of each, so the default is safe rather than convenient.
/// </para>
/// </remarks>
public sealed class SessionReplayOptions
{
    /// <summary>
    /// Percentage of RUM sessions recorded, 0 to 100. Defaults to 100.
    /// </summary>
    /// <remarks>
    /// Applied on top of <see cref="RumOptions.SessionSampleRate"/>, not instead of it: 20% of
    /// sessions kept and 50% of those recorded gives 10% of sessions with a replay.
    /// </remarks>
    public float SampleRate { get; init; } = 100;

    /// <summary>Masking for text and input. Defaults to <see cref="TextAndInputPrivacy.MaskAll"/>.</summary>
    public TextAndInputPrivacy TextAndInputPrivacy { get; init; } = TextAndInputPrivacy.MaskAll;

    /// <summary>Masking for images. Defaults to <see cref="ImagePrivacy.MaskAll"/>.</summary>
    public ImagePrivacy ImagePrivacy { get; init; } = ImagePrivacy.MaskAll;

    /// <summary>Masking for touches. Defaults to <see cref="TouchPrivacy.Hide"/>.</summary>
    public TouchPrivacy TouchPrivacy { get; init; } = TouchPrivacy.Hide;

    /// <summary>
    /// Begin recording as soon as the feature is enabled. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Set to <see langword="false"/> to enable the feature without recording, then call
    /// <see cref="ISessionReplay.StartRecording"/> once the user has agreed —
    /// <see cref="ISessionReplay.StopRecording"/> pauses again.
    /// </remarks>
    public bool StartRecordingImmediately { get; init; } = true;

    /// <summary>Send replay data somewhere other than the site's intake.</summary>
    public Uri? CustomEndpoint { get; init; }

    /// <summary>
    /// Reaches the native Session Replay configuration before the feature is enabled.
    /// </summary>
    /// <remarks>
    /// The argument is
    /// <c>Com.Datadog.Android.Sessionreplay.SessionReplayConfiguration.Builder</c> on Android and
    /// <c>DatadogSessionReplay.DDSessionReplayConfiguration</c> on iOS.
    /// <para>
    /// On Android the Material extension is registered before this runs, so a
    /// <c>AddExtensionSupport</c> call here adds to it rather than replacing it — which is what you
    /// want for the Compose extension, the only other one Datadog ships.
    /// </para>
    /// </remarks>
    public Action<object>? ConfigureNative { get; init; }
}
