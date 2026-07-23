using Android.App;
using Com.Datadog.Android;
using Com.Datadog.Android.Core.Configuration;
using Com.Datadog.Android.Rum;
using Com.Datadog.Android.Rum.Tracking;
using Com.Datadog.Android.Sessionreplay;
using Com.Datadog.Android.Sessionreplay.Material;
using Com.Datadog.Android.Trace;
using IO.Opentracing.Util;

// Every one of these collides with something in this file's own scope - the type being extended is
// called Datadog, its members are called Logs, Trace and SessionReplay, and the SDK's types have
// exactly those names too. Aliasing once here beats fully qualifying at each use.
using NativeDatadog = Com.Datadog.Android.Datadog;
using NativeLogs = Com.Datadog.Android.Log.Logs;
using NativeLogsConfiguration = Com.Datadog.Android.Log.LogsConfiguration;
using NativeRum = Com.Datadog.Android.Rum.Rum;
using NativeSessionReplay = Com.Datadog.Android.Sessionreplay.SessionReplay;
using NativeTrace = Com.Datadog.Android.Trace.Trace;
using NativeTrackingConsent = Com.Datadog.Android.Privacy.TrackingConsent;

namespace DatadogNet;

/// <summary>The Android implementation, over <c>DatadogNet.Android</c>.</summary>
/// <remarks>
/// dd-sdk-android has no <c>DatadogObjc</c> equivalent — each feature is its own module with its
/// own Kotlin builder — so this reaches seven packages where the iOS side reaches one.
/// </remarks>
public static partial class Datadog
{
    /// <summary>
    /// The verbosity value that silences the SDK's own logging.
    /// </summary>
    /// <remarks>
    /// dd-sdk-android's verbosity is a threshold compared against <c>android.util.Log</c>'s
    /// priority constants, which top out at <c>ASSERT</c> (7), and its own default is
    /// <see cref="int.MaxValue"/> — so "off" is expressed by a threshold nothing can reach rather
    /// than by a distinguished constant. iOS has a real <c>DDSDKVerbosityLevel.None</c>.
    /// </remarks>
    private const int VerbosityOff = int.MaxValue;

    private static partial bool PlatformIsSupported() => true;

    private static partial void PlatformInitialize(DatadogConfiguration configuration)
    {
        // Application.Context rather than an Activity, and rather than making the caller supply
        // one. The SDK keeps whatever it is given for the life of the process, so an Activity would
        // be leaked; and requiring a Context in the shared API would put an Android type in a
        // signature iOS also has to satisfy.
        var context = Application.Context
            ?? throw new InvalidOperationException(
                "Android.App.Application.Context is null, so Datadog cannot be initialised. This " +
                "happens when Initialize is called before the Application object exists - from a " +
                "static constructor of a type touched during process start, typically. Call it " +
                "from MauiProgram.CreateMauiApp or from your Application subclass's OnCreate.");

        var builder = new Configuration.Builder(
            clientToken: configuration.ClientToken,
            env: configuration.Env,
            variant: configuration.Variant,
            // Kotlin defaults `service` to the package name, and C# does not inherit Kotlin's
            // default arguments - but the parameter is @Nullable, so passing null still selects
            // that default rather than setting an empty service name.
            service: configuration.Service!);

        builder.UseSite(ToNativeSite(configuration.Site))
            .SetBatchSize(ToNativeBatchSize(configuration.BatchSize))
            .SetUploadFrequency(ToNativeUploadFrequency(configuration.UploadFrequency))
            .SetBatchProcessingLevel(ToNativeBatchProcessingLevel(configuration.BatchProcessingLevel))
            .SetCrashReportsEnabled(configuration.CrashReportsEnabled);

        if (configuration.FirstPartyHosts is { Count: > 0 } hosts)
        {
            // Only reaches dd-sdk-android's own OkHttp instrumentation, which a MAUI app using
            // HttpClient does not go through - DatadogHttpMessageHandler reads the same list from
            // the configuration and injects in the managed pipeline. Set anyway, so an app that
            // does use OkHttp directly gets the hosts it configured without a second declaration.
            var native = new Dictionary<string, ICollection<Com.Datadog.Android.Trace.TracingHeaderType>>(hosts.Count);

            foreach (var host in hosts)
            {
                native[host.Key] = [.. host.Value.Select(ToNativeHeaderType)];
            }

            builder.SetFirstPartyHostsWithHeaderType(native);
        }

        if (configuration.AdditionalConfiguration is { Count: > 0 } additional)
        {
            builder.SetAdditionalConfiguration(DatadogAttributes.From(additional));
        }

        configuration.ConfigureNative?.Invoke(builder);

        NativeDatadog.Initialize(context, builder.Build(), ToNativeConsent(configuration.TrackingConsent));
        NativeDatadog.Verbosity = ToNativeVerbosity(configuration.Verbosity);

        // Order matters and is this method's responsibility rather than the caller's: Session
        // Replay attaches to the RUM session and records nothing at all if RUM is not up yet.
        if (configuration.Rum is { } rum)
        {
            EnableRum(rum);
        }

        if (configuration.Logs is { } logs)
        {
            EnableLogs(logs);
        }

        if (configuration.Trace is { } trace)
        {
            EnableTrace(trace, configuration.Service);
        }

        if (configuration.SessionReplay is { } replay)
        {
            EnableSessionReplay(replay);
        }
    }

    private static void EnableRum(RumOptions options)
    {
        var builder = new RumConfiguration.Builder(options.ApplicationId)
            .SetSessionSampleRate(options.SessionSampleRate)
            .SetTelemetrySampleRate(options.TelemetrySampleRate)
            .TrackFrustrations(options.TrackFrustrations)
            .TrackBackgroundEvents(options.TrackBackgroundEvents)
            .TrackAnonymousUser(options.TrackAnonymousUser)
            .SetVitalsUpdateFrequency(ToNativeVitalsFrequency(options.VitalsUpdateFrequency));

        if (options.LongTaskThreshold is { } threshold)
        {
            builder.TrackLongTasks((long)threshold.TotalMilliseconds);
        }
        else
        {
            // dd-sdk-android's builder has no "off" switch for long tasks; a non-positive threshold
            // is how the SDK itself disables them.
            builder.TrackLongTasks(0);
        }

        if (options.TrackAutomaticInstrumentation)
        {
            builder.TrackUserInteractions();

            // MAUI draws every page into one Activity, so this reports a single view for the whole
            // app rather than per screen - which is why DatadogNet.Maui reports views from
            // navigation instead. It is still worth enabling: it is what reports app start, ANRs
            // and the application-level lifecycle.
            builder.UseViewTrackingStrategy(new ActivityViewTrackingStrategy(true));
        }
        else
        {
            builder.DisableUserInteractionTracking();
        }

        if (options.CustomEndpoint is { } endpoint)
        {
            builder.UseCustomEndpoint(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(builder);

        NativeRum.Enable(builder.Build());
    }

    private static void EnableLogs(LogsOptions options)
    {
        var builder = new NativeLogsConfiguration.Builder();

        if (options.CustomEndpoint is { } endpoint)
        {
            builder.UseCustomEndpoint(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(builder);

        NativeLogs.Enable(builder.Build());
    }

    private static void EnableTrace(TraceOptions options, string? service)
    {
        var builder = new TraceConfiguration.Builder()
            .SetNetworkInfoEnabled(options.NetworkInfoEnabled);

        if (options.CustomEndpoint is { } endpoint)
        {
            builder.UseCustomEndpoint(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(builder);

        NativeTrace.Enable(builder.Build());

        // 2.x tracing is OpenTracing: AndroidTracer implements io.opentracing.Tracer, and
        // GlobalTracer is where the rest of the process reaches it from. The sample rate, service
        // and header types live on the tracer rather than on the feature configuration, which is
        // the reverse of iOS - hence the split between what went on the builder above and what goes
        // on this one.
        var tracer = new AndroidTracer.Builder()
            .SetSampleRate(options.SampleRate)
            .SetBundleWithRumEnabled(options.BundleWithRumEnabled)
            .SetTracingHeaderTypes(
                new HashSet<Com.Datadog.Android.Trace.TracingHeaderType>(
                    options.HeaderTypes.Select(ToNativeHeaderType)));

        if ((options.Service ?? service) is { Length: > 0 } traceService)
        {
            tracer.SetService(traceService);
        }

        if (options.GlobalTags is { Count: > 0 } tags)
        {
            foreach (var tag in tags)
            {
                tracer.AddTag(tag.Key, tag.Value);
            }
        }

        // RegisterIfAbsent rather than Register: registering twice throws, and a MAUI app on
        // Android can have its entry point run again after the process is recreated.
        GlobalTracer.RegisterIfAbsent(tracer.Build()!);
    }

    private static void EnableSessionReplay(SessionReplayOptions options)
    {
        var builder = new SessionReplayConfiguration.Builder(options.SampleRate)
            .SetTextAndInputPrivacy(ToNativeTextPrivacy(options.TextAndInputPrivacy))
            .SetImagePrivacy(ToNativeImagePrivacy(options.ImagePrivacy))
            .SetTouchPrivacy(ToNativeTouchPrivacy(options.TouchPrivacy))
            .StartRecordingImmediately(options.StartRecordingImmediately)
            // MAUI's Android handlers are built on Material Components, so without this every
            // MAUI-drawn control records as an unstyled box. This is the single biggest difference
            // between a useful Session Replay of a MAUI app and a useless one, and is why
            // DatadogNet.SessionReplayMaterial.Android is a dependency of this package rather than
            // an optional extra.
            .AddExtensionSupport(new MaterialExtensionSupport());

        if (options.CustomEndpoint is { } endpoint)
        {
            builder.UseCustomEndpoint(endpoint.ToString());
        }

        // After AddExtensionSupport, so a ConfigureNative that registers the Compose extension adds
        // to Material rather than having to re-register it.
        options.ConfigureNative?.Invoke(builder);

        NativeSessionReplay.Enable(builder.Build());
    }

    private static partial void PlatformSetTrackingConsent(TrackingConsent consent) =>
        NativeDatadog.SetTrackingConsent(ToNativeConsent(consent));

    private static partial DatadogVerbosity PlatformGetVerbosity() => NativeDatadog.Verbosity switch
    {
        <= 3 => DatadogVerbosity.Debug,
        <= 5 => DatadogVerbosity.Warn,
        6 => DatadogVerbosity.Error,
        7 => DatadogVerbosity.Critical,
        _ => DatadogVerbosity.None,
    };

    private static partial void PlatformSetVerbosity(DatadogVerbosity verbosity) =>
        NativeDatadog.Verbosity = ToNativeVerbosity(verbosity);

    private static partial void PlatformSetUser(
        string id,
        string? name,
        string? email,
        IReadOnlyDictionary<string, object?>? extraInfo) =>
        NativeDatadog.SetUserInfo(id, name!, email!, DatadogAttributes.From(extraInfo));

    private static partial void PlatformAddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        NativeDatadog.AddUserProperties(DatadogAttributes.From(extraInfo));

    private static partial void PlatformClearUser() => NativeDatadog.ClearUserInfo();

    private static partial void PlatformSetAccount(
        string id,
        string? name,
        IReadOnlyDictionary<string, object?>? extraInfo) =>
        NativeDatadog.SetAccountInfo(id, name!, DatadogAttributes.From(extraInfo));

    private static partial void PlatformAddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        NativeDatadog.AddAccountExtraInfo(DatadogAttributes.From(extraInfo));

    private static partial void PlatformClearAccount() => NativeDatadog.ClearAccountInfo();

    private static partial void PlatformClearAllData() => NativeDatadog.ClearAllData();

    private static partial void PlatformStop() => NativeDatadog.StopInstance();

    private static partial IRumMonitor CreateRum() => new AndroidRumMonitor();

    private static partial IDatadogLogs CreateLogs() => new AndroidLogs();

    private static partial IDatadogTracer CreateTracer() => new AndroidTracerAdapter();

    private static partial ISessionReplay CreateSessionReplay() => new AndroidSessionReplay();

    private static Com.Datadog.Android.DatadogSite ToNativeSite(DatadogSite site) => site switch
    {
        DatadogSite.Us1 => Com.Datadog.Android.DatadogSite.Us1!,
        DatadogSite.Us3 => Com.Datadog.Android.DatadogSite.Us3!,
        DatadogSite.Us5 => Com.Datadog.Android.DatadogSite.Us5!,
        DatadogSite.Eu1 => Com.Datadog.Android.DatadogSite.Eu1!,
        DatadogSite.Ap1 => Com.Datadog.Android.DatadogSite.Ap1!,
        DatadogSite.Ap2 => Com.Datadog.Android.DatadogSite.Ap2!,
        DatadogSite.Us1Fed => Com.Datadog.Android.DatadogSite.Us1Fed!,
        _ => throw new ArgumentOutOfRangeException(nameof(site), site, "Unknown Datadog site."),
    };

    private static Com.Datadog.Android.Core.Configuration.BatchSize ToNativeBatchSize(
        DatadogNet.BatchSize size) => size switch
    {
        DatadogNet.BatchSize.Small => Com.Datadog.Android.Core.Configuration.BatchSize.Small!,
        DatadogNet.BatchSize.Large => Com.Datadog.Android.Core.Configuration.BatchSize.Large!,
        _ => Com.Datadog.Android.Core.Configuration.BatchSize.Medium!,
    };

    private static Com.Datadog.Android.Core.Configuration.UploadFrequency ToNativeUploadFrequency(
        DatadogNet.UploadFrequency frequency) => frequency switch
    {
        DatadogNet.UploadFrequency.Frequent => Com.Datadog.Android.Core.Configuration.UploadFrequency.Frequent!,
        DatadogNet.UploadFrequency.Rare => Com.Datadog.Android.Core.Configuration.UploadFrequency.Rare!,
        _ => Com.Datadog.Android.Core.Configuration.UploadFrequency.Average!,
    };

    private static Com.Datadog.Android.Core.Configuration.BatchProcessingLevel ToNativeBatchProcessingLevel(
        DatadogNet.BatchProcessingLevel level) => level switch
    {
        DatadogNet.BatchProcessingLevel.Low => Com.Datadog.Android.Core.Configuration.BatchProcessingLevel.Low!,
        DatadogNet.BatchProcessingLevel.High => Com.Datadog.Android.Core.Configuration.BatchProcessingLevel.High!,
        _ => Com.Datadog.Android.Core.Configuration.BatchProcessingLevel.Medium!,
    };

    private static int ToNativeVerbosity(DatadogVerbosity verbosity) => verbosity switch
    {
        DatadogVerbosity.Debug => (int)Android.Util.LogPriority.Debug,
        DatadogVerbosity.Warn => (int)Android.Util.LogPriority.Warn,
        DatadogVerbosity.Error => (int)Android.Util.LogPriority.Error,
        DatadogVerbosity.Critical => (int)Android.Util.LogPriority.Assert,
        _ => VerbosityOff,
    };

    private static NativeTrackingConsent ToNativeConsent(TrackingConsent consent) => consent switch
    {
        TrackingConsent.Granted => NativeTrackingConsent.Granted!,
        TrackingConsent.NotGranted => NativeTrackingConsent.NotGranted!,
        _ => NativeTrackingConsent.Pending!,
    };

    private static Com.Datadog.Android.Rum.Configuration.VitalsUpdateFrequency ToNativeVitalsFrequency(
        DatadogNet.VitalsUpdateFrequency frequency) => frequency switch
    {
        DatadogNet.VitalsUpdateFrequency.Frequent =>
            Com.Datadog.Android.Rum.Configuration.VitalsUpdateFrequency.Frequent!,
        DatadogNet.VitalsUpdateFrequency.Rare =>
            Com.Datadog.Android.Rum.Configuration.VitalsUpdateFrequency.Rare!,
        DatadogNet.VitalsUpdateFrequency.Never =>
            Com.Datadog.Android.Rum.Configuration.VitalsUpdateFrequency.Never!,
        _ => Com.Datadog.Android.Rum.Configuration.VitalsUpdateFrequency.Average!,
    };

    private static Com.Datadog.Android.Trace.TracingHeaderType ToNativeHeaderType(TracingHeaderType type) => type switch
    {
        TracingHeaderType.B3 => Com.Datadog.Android.Trace.TracingHeaderType.B3!,
        TracingHeaderType.B3Multi => Com.Datadog.Android.Trace.TracingHeaderType.B3multi!,
        TracingHeaderType.TraceContext => Com.Datadog.Android.Trace.TracingHeaderType.Tracecontext!,
        _ => Com.Datadog.Android.Trace.TracingHeaderType.Datadog!,
    };

    private static Com.Datadog.Android.Sessionreplay.TextAndInputPrivacy ToNativeTextPrivacy(
        DatadogNet.TextAndInputPrivacy privacy) => privacy switch
    {
        DatadogNet.TextAndInputPrivacy.MaskAllInputs =>
            Com.Datadog.Android.Sessionreplay.TextAndInputPrivacy.MaskAllInputs!,
        DatadogNet.TextAndInputPrivacy.MaskSensitiveInputs =>
            Com.Datadog.Android.Sessionreplay.TextAndInputPrivacy.MaskSensitiveInputs!,
        _ => Com.Datadog.Android.Sessionreplay.TextAndInputPrivacy.MaskAll!,
    };

    private static Com.Datadog.Android.Sessionreplay.ImagePrivacy ToNativeImagePrivacy(
        DatadogNet.ImagePrivacy privacy) => privacy switch
    {
        // See ImagePrivacy.MaskContentImages: Android decides by size and iOS by whether the image
        // was bundled with the app. Same intent, different rule, and the only Session Replay
        // setting whose behaviour is not identical across the two.
        DatadogNet.ImagePrivacy.MaskContentImages =>
            Com.Datadog.Android.Sessionreplay.ImagePrivacy.MaskLargeOnly!,
        DatadogNet.ImagePrivacy.MaskNone =>
            Com.Datadog.Android.Sessionreplay.ImagePrivacy.MaskNone!,
        _ => Com.Datadog.Android.Sessionreplay.ImagePrivacy.MaskAll!,
    };

    private static Com.Datadog.Android.Sessionreplay.TouchPrivacy ToNativeTouchPrivacy(
        DatadogNet.TouchPrivacy privacy) =>
        privacy == DatadogNet.TouchPrivacy.Show
            ? Com.Datadog.Android.Sessionreplay.TouchPrivacy.Show!
            : Com.Datadog.Android.Sessionreplay.TouchPrivacy.Hide!;
}
