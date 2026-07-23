using DatadogSessionReplay;

namespace DatadogNet;

/// <summary>Session Replay over <c>DDSessionReplay</c>.</summary>
/// <remarks>
/// Both members are static on iOS and instance members on Android — <c>SessionReplay.Instance</c>
/// there — which is one of the asymmetries this façade exists to absorb.
/// </remarks>
internal sealed class IosSessionReplay : ISessionReplay
{
    public bool IsEnabled => Datadog.Configuration?.SessionReplay is not null;

    public void StartRecording() => DDSessionReplay.StartRecording();

    public void StopRecording() => DDSessionReplay.StopRecording();
}
