namespace DatadogNet;

/// <summary>
/// Controls Session Replay recording after the feature has been enabled.
/// </summary>
/// <remarks>
/// Reached through <see cref="Datadog.SessionReplay"/>. Enabling the feature is
/// <see cref="DatadogConfiguration.SessionReplay"/>'s job; this is only for starting and stopping
/// the recording within a session that already has it.
/// <para>
/// The pair exists so an app can enable Session Replay at startup — which it has to, since the
/// feature cannot be turned on later — and still not record until the user has agreed. Set
/// <see cref="SessionReplayOptions.StartRecordingImmediately"/> to <see langword="false"/> and call
/// <see cref="StartRecording"/> when they do.
/// </para>
/// </remarks>
public interface ISessionReplay
{
    /// <summary>
    /// Whether Session Replay was enabled by <see cref="DatadogConfiguration.SessionReplay"/>.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Begins recording.</summary>
    void StartRecording();

    /// <summary>
    /// Pauses recording.
    /// </summary>
    /// <remarks>
    /// The session continues and RUM events keep flowing; only the replay stops. Worth calling
    /// around a screen you have decided never to record — a document viewer, a camera preview —
    /// where masking is not enough.
    /// </remarks>
    void StopRecording();
}
