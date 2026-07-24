using DatadogNet;
using DatadogNet.Maui;

namespace DatadogNet.Maui.Sample;

/// <summary>
/// Everything this sample does to set Datadog up, in one place.
/// </summary>
/// <remarks>
/// This is the shape most apps end up with: one <c>UseDatadog</c> call, one
/// <c>CrashReporting.Enable</c>, and nothing else. Everything after that goes through
/// <see cref="Datadog.Rum"/>, <see cref="Datadog.Logger"/> and <see cref="Datadog.Tracer"/>, or
/// through the interfaces injected into a page.
/// </remarks>
public static class MauiProgram
{
    /// <summary>
    /// Replace these with your own values from the Datadog UI.
    /// </summary>
    /// <remarks>
    /// The placeholders let the sample run and report exactly the way a configured app would; the
    /// events simply never arrive. Every feature is also pointed at a custom endpoint on localhost,
    /// so nothing leaves the device even if you paste in a real token by accident — delete the
    /// <c>CustomEndpoint</c> lines to upload to Datadog for real.
    /// </remarks>
    private const string ClientToken = "<CLIENT_TOKEN>";

    private const string RumApplicationId = "<RUM_APPLICATION_ID>";

    private static readonly Uri LocalEndpoint = new("http://localhost:9/");

    /// <summary>Whether real credentials were supplied, so events actually reach Datadog.</summary>
    public static bool IsConfigured => !ClientToken.StartsWith('<');

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()

            // First, before anything that could itself throw. The SDK is initialised during this
            // call rather than when the app starts, which is as early as shared MAUI code can
            // manage - and crash reporting only covers what happens after the SDK is up.
            .UseDatadog(
                new DatadogConfiguration
                {
                    ClientToken = ClientToken,
                    Env = "sample",
                    Service = "datadognet-maui-sample",

                    // Pick the site your organisation is on. Sending to the wrong one is the most
                    // common reason nothing ever shows up in Datadog.
                    Site = DatadogSite.Us1,

                    // Granted because this is a sample. A real app starts at Pending - which
                    // collects and holds without uploading - and calls
                    // Datadog.SetTrackingConsent once the user has answered a prompt.
                    TrackingConsent = TrackingConsent.Granted,

                    // The SDK's own diagnostics, to logcat or the Xcode console. Worth leaving on
                    // while wiring this in for the first time; it is where "your client token is
                    // invalid" appears.
                    Verbosity = DatadogVerbosity.Warn,

                    // Only these hosts get tracing headers. Everything else is still reported as a
                    // RUM resource - you want to see how slow a third-party API is - but never
                    // given a trace id.
                    FirstPartyHosts = new Dictionary<string, IReadOnlyList<TracingHeaderType>>
                    {
                        ["localhost"] = [TracingHeaderType.Datadog, TracingHeaderType.TraceContext],
                    },

                    Rum = new RumOptions
                    {
                        ApplicationId = RumApplicationId,
                        SessionSampleRate = 100,
                        TrackFrustrations = true,
                        TrackBackgroundEvents = true,
                        CustomEndpoint = LocalEndpoint,

                        // The façade covers what both SDKs share; ConfigureNative reaches the
                        // rest, at the one moment it can still be set. One platform-only setting
                        // each side, as a worked example - this is the only conditional
                        // compilation in the sample, and it is the escape hatch's nature.
                        ConfigureNative = native =>
                        {
#if ANDROID
                            ((Com.Datadog.Android.Rum.RumConfiguration.Builder)native)
                                .TrackNonFatalAnrs(true);
#elif IOS || MACCATALYST
                            ((DatadogRUM.DDRUMConfiguration)native).TrackWatchdogTerminations = true;
#endif
                        },
                    },

                    Logs = new LogsOptions { CustomEndpoint = LocalEndpoint },

                    Trace = new TraceOptions
                    {
                        SampleRate = 100,
                        NetworkInfoEnabled = true,
                        CustomEndpoint = LocalEndpoint,
                    },

                    // Everything masked. These levels decide what is redacted on the device, before
                    // anything is uploaded - loosen them deliberately, not by default.
                    SessionReplay = new SessionReplayOptions
                    {
                        SampleRate = 100,
                        TextAndInputPrivacy = TextAndInputPrivacy.MaskAll,
                        ImagePrivacy = ImagePrivacy.MaskAll,
                        TouchPrivacy = TouchPrivacy.Hide,
                        CustomEndpoint = LocalEndpoint,

                        // The consent-gated shape: the feature is enabled but records nothing
                        // until Datadog.SessionReplay.StartRecording() - the "Start / resume
                        // recording" button - which is where an app puts the moment its user
                        // agreed to being recorded.
                        StartRecordingImmediately = false,
                    },
                },
                new DatadogMauiOptions
                {
                    Logger = new LoggerOptions
                    {
                        Name = "sample",
                        NetworkInfoEnabled = true,
                        // Also to logcat / the Xcode console, so the sample's logs are visible
                        // without a Datadog account. This is the SDK's own console output, which is
                        // why the sample needs no separate logging provider for it.
                        PrintToConsole = true,
                    },
                });

        // Installs a signal handler, so it is deliberately after initialisation and deliberately in
        // a package of its own. On iOS this is *all* of crash reporting; on Android it adds native
        // (NDK) crashes on top of the JVM crashes the core already reports.
        CrashReporting.Enable();

        // Every request through this client becomes a RUM resource, wrapped in a span, with tracing
        // headers attached on the first-party hosts declared above. Neither native SDK's automatic
        // network instrumentation can see an HttpClient call.
        builder.Services
            .AddHttpClient("sample", client => client.Timeout = TimeSpan.FromSeconds(5))
            .AddDatadogTracking();

        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
