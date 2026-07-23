using DatadogCore;
using DatadogInternal;
using DatadogLogs;
using DatadogRUM;
using DatadogSessionReplay;
using DatadogTrace;
using Foundation;

// DatadogCore declares a TrackingConsent enum of its own - DatadogNet.iOS added it, because the
// bound DDTrackingConsent is a class of static factory methods rather than an enum - and it collides
// with ours. Aliased rather than fully qualified at every use.
using NativeConsent = DatadogCore.TrackingConsent;

namespace DatadogNet;

/// <summary>The iOS implementation, over <c>DatadogNet.iOS</c> 3.x.</summary>
/// <remarks>
/// Five namespaces where the 2.x implementation had one. dd-sdk-ios 3.0 dissolved the
/// <c>DatadogObjc</c> framework that used to re-export the whole SDK: the <c>DD*</c> types now live
/// in the module they belong to. Nothing about the API changed in the move — the same selectors are
/// there — but every <c>using</c> did, and a handful of members that were properties became methods.
/// </remarks>
public static partial class Datadog
{
    private static partial bool PlatformIsSupported() => true;

    private static partial void PlatformInitialize(DatadogConfiguration configuration)
    {
        var native = new DDConfiguration(configuration.ClientToken, configuration.Env)
        {
            Site = ToNativeSite(configuration.Site),
            BatchSize = ToNativeBatchSize(configuration.BatchSize),
            UploadFrequency = ToNativeUploadFrequency(configuration.UploadFrequency),
            BatchProcessingLevel = ToNativeBatchProcessingLevel(configuration.BatchProcessingLevel),
        };

        if (!string.IsNullOrEmpty(configuration.Service))
        {
            native.Service = configuration.Service;
        }

        if (configuration.AdditionalConfiguration is { Count: > 0 } additional)
        {
            native.AdditionalConfiguration = DatadogAttributes.From(additional);
        }

        // DatadogConfiguration.CrashReportsEnabled has no counterpart here and is documented as
        // Android-only: on iOS crash reporting is an entire separate framework rather than a switch,
        // and in 3.x its engine is KSCrash. See the DatadogNet.CrashReporting package.
        //
        // FirstPartyHosts has no configuration-level counterpart either - it lives on
        // DDURLSessionInstrumentation, which only applies to an NSURLSession the SDK has
        // instrumented, and NSUrlSessionHandler owns its delegate rather than exposing it.
        // DatadogHttpMessageHandler reads the host list from the configuration and injects in the
        // managed pipeline, which is what makes first-party hosts work identically on both platforms.
        configuration.ConfigureNative?.Invoke(native);

        DDDatadog.InitializeWithConfiguration(native, ToNativeConsent(configuration.TrackingConsent));

        // A method in 3.x, where 2.x had a VerbosityLevel property, and the enum moved to
        // DatadogInternal and was renamed from DDSDKVerbosityLevel to DDCoreLoggerLevel.
        DDDatadog.SetVerbosityLevel(ToNativeVerbosity(configuration.Verbosity));

        // Order matters and is this method's responsibility rather than the caller's: RUM has to be
        // enabled before Session Replay, which attaches to the RUM session and silently records
        // nothing otherwise.
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
        var native = new DDRUMConfiguration(options.ApplicationId)
        {
            SessionSampleRate = options.SessionSampleRate,
            TelemetrySampleRate = options.TelemetrySampleRate,
            TrackFrustrations = options.TrackFrustrations,
            TrackBackgroundEvents = options.TrackBackgroundEvents,
            TrackAnonymousUser = options.TrackAnonymousUser,
            VitalsUpdateFrequency = ToNativeVitalsFrequency(options.VitalsUpdateFrequency),
        };

        // dd-sdk-ios disables long-task tracking by setting the threshold to zero rather than by a
        // flag, which is not obvious from the property name.
        native.LongTaskThreshold = options.LongTaskThreshold is { } threshold ? threshold.TotalSeconds : 0;

        if (options.TrackAutomaticInstrumentation)
        {
            // MAUI renders through UIKit, so the default predicates report views per
            // UIViewController and capture taps with no per-page code. Views are still worth
            // reporting yourself - a Shell page is not reliably one view controller - which is what
            // DatadogNet.Maui's navigation tracking does.
            native.UiKitViewsPredicate = new DDDefaultUIKitRUMViewsPredicate();
            native.UiKitActionsPredicate = new DDDefaultUIKitRUMActionsPredicate();
        }

        if (options.CustomEndpoint is { } endpoint)
        {
            native.CustomEndpoint = new NSUrl(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(native);

        DDRUM.EnableWith(native);
    }

    private static void EnableLogs(LogsOptions options)
    {
        var native = new DDLogsConfiguration(
            options.CustomEndpoint is { } endpoint ? new NSUrl(endpoint.ToString()) : null);

        options.ConfigureNative?.Invoke(native);

        DDLogs.EnableWith(native);
    }

    private static void EnableTrace(TraceOptions options, string? service)
    {
        var native = new DDTraceConfiguration
        {
            SampleRate = options.SampleRate,
            BundleWithRumEnabled = options.BundleWithRumEnabled,
            NetworkInfoEnabled = options.NetworkInfoEnabled,
        };

        if ((options.Service ?? service) is { Length: > 0 } traceService)
        {
            native.Service = traceService;
        }

        if (options.GlobalTags is { Count: > 0 } tags)
        {
            var converted = new Dictionary<string, object?>(tags.Count);
            foreach (var tag in tags)
            {
                converted[tag.Key] = tag.Value;
            }

            native.Tags = DatadogAttributes.From(converted);
        }

        if (options.CustomEndpoint is { } endpoint)
        {
            native.CustomEndpoint = new NSUrl(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(native);

        DDTrace.EnableWith(native);
    }

    private static void EnableSessionReplay(SessionReplayOptions options)
    {
        var native = new DDSessionReplayConfiguration(
            options.SampleRate,
            ToNativeTextPrivacy(options.TextAndInputPrivacy),
            ToNativeImagePrivacy(options.ImagePrivacy),
            ToNativeTouchPrivacy(options.TouchPrivacy))
        {
            StartRecordingImmediately = options.StartRecordingImmediately,
        };

        if (options.CustomEndpoint is { } endpoint)
        {
            native.CustomEndpoint = new NSUrl(endpoint.ToString());
        }

        options.ConfigureNative?.Invoke(native);

        DDSessionReplay.EnableWith(native);
    }

    private static partial void PlatformSetTrackingConsent(TrackingConsent consent) =>
        DDDatadog.SetTrackingConsent(ToManagedNativeConsent(consent));

    private static partial DatadogVerbosity PlatformGetVerbosity() =>
        DDDatadog.VerbosityLevel() switch
        {
            DDCoreLoggerLevel.Debug => DatadogVerbosity.Debug,
            DDCoreLoggerLevel.Warn => DatadogVerbosity.Warn,
            DDCoreLoggerLevel.Error => DatadogVerbosity.Error,
            DDCoreLoggerLevel.Critical => DatadogVerbosity.Critical,
            _ => DatadogVerbosity.None,
        };

    private static partial void PlatformSetVerbosity(DatadogVerbosity verbosity) =>
        DDDatadog.SetVerbosityLevel(ToNativeVerbosity(verbosity));

    private static partial void PlatformSetUser(
        string id,
        string? name,
        string? email,
        IReadOnlyDictionary<string, object?>? extraInfo) =>
        DDDatadog.SetUserInfoWithUserId(id, name, email, DatadogAttributes.From(extraInfo));

    private static partial void PlatformAddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        DDDatadog.AddUserExtraInfo(DatadogAttributes.From(extraInfo));

    private static partial void PlatformClearUser() => DDDatadog.ClearUserInfo();

    private static partial void PlatformSetAccount(
        string id,
        string? name,
        IReadOnlyDictionary<string, object?>? extraInfo) =>
        DDDatadog.SetAccountInfoWithAccountId(id, name, DatadogAttributes.From(extraInfo));

    private static partial void PlatformAddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        DDDatadog.AddAccountExtraInfo(DatadogAttributes.From(extraInfo));

    private static partial void PlatformClearAccount() => DDDatadog.ClearAccountInfo();

    private static partial void PlatformClearAllData() => DDDatadog.ClearAllData();

    private static partial void PlatformStop() => DDDatadog.StopInstance();

    private static partial IRumMonitor CreateRum() => new IosRumMonitor();

    private static partial IDatadogLogs CreateLogs() => new IosLogs();

    private static partial IDatadogTracer CreateTracer() => new IosTracer();

    private static partial ISessionReplay CreateSessionReplay() => new IosSessionReplay();

    /// <remarks>
    /// Methods in 3.x, where 2.x exposed these as static properties. <c>Us2_fed()</c> is new in 3.x
    /// and dd-sdk-android has it too, so unlike in 2.x it is on <see cref="DatadogSite"/>.
    /// </remarks>
    private static DDSite ToNativeSite(DatadogSite site) => site switch
    {
        DatadogSite.Us1 => DDSite.Us1(),
        DatadogSite.Us3 => DDSite.Us3(),
        DatadogSite.Us5 => DDSite.Us5(),
        DatadogSite.Eu1 => DDSite.Eu1(),
        DatadogSite.Ap1 => DDSite.Ap1(),
        DatadogSite.Ap2 => DDSite.Ap2(),
        DatadogSite.Us1Fed => DDSite.Us1_fed(),
        DatadogSite.Us2Fed => DDSite.Us2_fed(),
        _ => throw new ArgumentOutOfRangeException(nameof(site), site, "Unknown Datadog site."),
    };

    private static DDBatchSize ToNativeBatchSize(BatchSize size) => size switch
    {
        BatchSize.Small => DDBatchSize.Small,
        BatchSize.Large => DDBatchSize.Large,
        _ => DDBatchSize.Medium,
    };

    private static DDUploadFrequency ToNativeUploadFrequency(UploadFrequency frequency) => frequency switch
    {
        UploadFrequency.Frequent => DDUploadFrequency.Frequent,
        UploadFrequency.Rare => DDUploadFrequency.Rare,
        _ => DDUploadFrequency.Average,
    };

    private static DDBatchProcessingLevel ToNativeBatchProcessingLevel(BatchProcessingLevel level) => level switch
    {
        BatchProcessingLevel.Low => DDBatchProcessingLevel.Low,
        BatchProcessingLevel.High => DDBatchProcessingLevel.High,
        _ => DDBatchProcessingLevel.Medium,
    };

    private static DDCoreLoggerLevel ToNativeVerbosity(DatadogVerbosity verbosity) => verbosity switch
    {
        DatadogVerbosity.Debug => DDCoreLoggerLevel.Debug,
        DatadogVerbosity.Warn => DDCoreLoggerLevel.Warn,
        DatadogVerbosity.Error => DDCoreLoggerLevel.Error,
        DatadogVerbosity.Critical => DDCoreLoggerLevel.Critical,
        _ => DDCoreLoggerLevel.Critical,
    };

    private static DDTrackingConsent ToNativeConsent(TrackingConsent consent) => consent switch
    {
        TrackingConsent.Granted => DDTrackingConsent.Granted(),
        TrackingConsent.NotGranted => DDTrackingConsent.NotGranted(),
        _ => DDTrackingConsent.Pending(),
    };

    private static NativeConsent ToManagedNativeConsent(TrackingConsent consent) => consent switch
    {
        TrackingConsent.Granted => NativeConsent.Granted,
        TrackingConsent.NotGranted => NativeConsent.NotGranted,
        _ => NativeConsent.Pending,
    };

    private static DDRUMVitalsFrequency ToNativeVitalsFrequency(VitalsUpdateFrequency frequency) => frequency switch
    {
        VitalsUpdateFrequency.Frequent => DDRUMVitalsFrequency.Frequent,
        VitalsUpdateFrequency.Rare => DDRUMVitalsFrequency.Rare,
        VitalsUpdateFrequency.Never => DDRUMVitalsFrequency.Never,
        _ => DDRUMVitalsFrequency.Average,
    };

    private static DDTextAndInputPrivacyLevel ToNativeTextPrivacy(TextAndInputPrivacy privacy) => privacy switch
    {
        TextAndInputPrivacy.MaskAllInputs => DDTextAndInputPrivacyLevel.MaskAllInputs,
        TextAndInputPrivacy.MaskSensitiveInputs => DDTextAndInputPrivacyLevel.MaskSensitiveInputs,
        _ => DDTextAndInputPrivacyLevel.MaskAll,
    };

    private static DDImagePrivacyLevel ToNativeImagePrivacy(ImagePrivacy privacy) => privacy switch
    {
        // See ImagePrivacy.MaskContentImages: iOS decides by "was this bundled with the app" and
        // Android by "is this bigger than an icon". Same intent, different rule, and the only
        // Session Replay setting whose behaviour is not identical across the two.
        ImagePrivacy.MaskContentImages => DDImagePrivacyLevel.MaskNonBundledOnly,
        ImagePrivacy.MaskNone => DDImagePrivacyLevel.MaskNone,
        _ => DDImagePrivacyLevel.MaskAll,
    };

    private static DDTouchPrivacyLevel ToNativeTouchPrivacy(TouchPrivacy privacy) =>
        privacy == TouchPrivacy.Show ? DDTouchPrivacyLevel.Show : DDTouchPrivacyLevel.Hide;
}
