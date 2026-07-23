using NativeDatadog = Com.Datadog.Android.Datadog;
using NativeSessionReplay = Com.Datadog.Android.Sessionreplay.SessionReplay;

namespace DatadogNet;

/// <summary>Session Replay over <c>SessionReplay</c>.</summary>
/// <remarks>
/// <c>enable</c> is static but <c>startRecording</c> and <c>stopRecording</c> are instance members
/// taking an <c>SdkCore</c> — an asymmetry that is upstream's, not the binding's. iOS has all three
/// as statics taking nothing.
/// </remarks>
internal sealed class AndroidSessionReplay : ISessionReplay
{
    public bool IsEnabled => Datadog.Configuration?.SessionReplay is not null;

    public void StartRecording()
    {
        if (NativeDatadog.Instance is { } core)
        {
            NativeSessionReplay.Instance!.StartRecording(core);
        }
    }

    public void StopRecording()
    {
        if (NativeDatadog.Instance is { } core)
        {
            NativeSessionReplay.Instance!.StopRecording(core);
        }
    }
}
