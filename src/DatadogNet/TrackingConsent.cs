namespace DatadogNet;

/// <summary>
/// Whether the SDK may collect and upload data about this user.
/// </summary>
/// <remarks>
/// Consent is set as part of <see cref="Datadog.Initialize"/> and can be changed at any time with
/// <see cref="Datadog.SetTrackingConsent"/>. It is enforced inside the SDK, on the device, before
/// anything is written to disk — it is not a flag the uploader consults.
/// </remarks>
public enum TrackingConsent
{
    /// <summary>
    /// Collect events and hold them on the device without uploading, until consent is granted or
    /// refused.
    /// </summary>
    /// <remarks>
    /// This is what a prompt-on-first-launch flow wants, and it is the default: the events from
    /// before the user answered the prompt are kept, and are uploaded retroactively if the answer
    /// is yes and discarded if it is no. Initialising as <see cref="Granted"/> and asking
    /// afterwards uploads data the user has not yet agreed to.
    /// </remarks>
    Pending,

    /// <summary>Collect and upload.</summary>
    Granted,

    /// <summary>
    /// Collect nothing, and discard anything collected while consent was
    /// <see cref="Pending"/>.
    /// </summary>
    NotGranted,
}
