namespace DatadogNet.DeviceTests;

/// <summary>A single on-device check. Throws to fail.</summary>
/// <param name="Name">Human readable name, reported to the platform log.</param>
/// <param name="Execute">Runs the check.</param>
public sealed record SmokeTest(string Name, Func<Task> Execute)
{
    public SmokeTest(string name, Action execute)
        : this(name, () =>
        {
            execute();
            return Task.CompletedTask;
        })
    {
    }
}

/// <summary>
/// End-to-end checks that only mean anything on a real device or simulator: they load the native
/// Datadog frameworks out of the packaged bindings and drive the real SDK through this façade.
/// </summary>
/// <remarks>
/// <b>This file is the repository's thesis stated as a test.</b> There is one copy of it, it uses
/// nothing but the cross-platform API, and it runs unchanged on an Android emulator and an iOS
/// simulator. If the façade did not actually unify dd-sdk-android and dd-sdk-ios, it could not
/// compile for both heads — never mind pass on both.
/// <para>
/// Nothing here reaches Datadog. The client token is fake and every feature is pointed at a custom
/// endpoint on localhost, so the SDK batches events to disk and its uploads fail locally rather
/// than sending junk to a real intake from CI.
/// </para>
/// <para>
/// The checks are ordered, and a failure early on cascades — which is the intent, since the first
/// failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    private const string ClientToken = "fake-client-token-for-e2e-only";

    private const string RumApplicationId = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// Where the SDK is told to upload to. Nothing listens on this port; the SDK retries in the
    /// background and never throws, which is exactly the isolation wanted here.
    /// </summary>
    private static readonly Uri LocalEndpoint = new("http://localhost:9/");

    /// <summary>Writes a line to the platform log. Set by each head.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

    /// <summary>Every check, in the order they must run.</summary>
    public static SmokeTest[] All =>
    [
        new("the façade reports this platform as supported", PlatformIsSupported),
        new("the API is safe to call before Initialize", ApiIsSafeBeforeInitialize),
        new("a malformed configuration is rejected", MalformedConfigurationIsRejected),
        new("initializes the SDK and enables every feature", InitializesEverything),
        new("sets verbosity, consent, user and account info", SetsSdkLevelState),
        new("drives a RUM view, action and error through the scope", DrivesRum),
        new("reports every attribute type without throwing", ReportsEveryAttributeType),
        new("rejects an attribute value with no native representation", RejectsUnconvertibleAttribute),
        new("drives RUM resources, timings and feature flags", DrivesRumResources),
        new("manages session-wide RUM attributes", ManagesGlobalRumAttributes),
        new("reads the current RUM session id", ReadsCurrentSessionId),
        new("writes a log at every level", WritesEveryLogLevel),
        new("attaches an exception to a log entry", AttachesExceptionToLog),
        new("manages logger attributes and tags", ManagesLoggerAttributesAndTags),
        new("starts, tags, activates and finishes a span", DrivesTracing),
        new("produces trace ids and propagation headers", ProducesTraceHeaders),
        new("reports an http request as a RUM resource", ReportsHttpRequest),
        new("starts and stops Session Replay recording", ControlsSessionReplay),
        new("enables crash reporting", EnablesCrashReporting),
        new("stops the RUM session and the SDK instance", StopsCleanly),
    ];

    private static void Report(string message) => Reporter(message);

    private static void PlatformIsSupported()
    {
        // False here would mean the neutral no-op assembly was selected for a platform head, which
        // would make every check below pass while measuring nothing at all.
        Assert(Datadog.IsSupported, "Datadog.IsSupported is false on a platform that has an SDK.");
        Assert(!Datadog.IsInitialized, "Datadog.IsInitialized is true before Initialize was called.");
    }

    private static void ApiIsSafeBeforeInitialize()
    {
        // Documented behaviour, and the reason shared MAUI code needs no guards: instrumentation
        // that crashes the app it is measuring is worse than instrumentation that is missing. Both
        // native SDKs log a warning and drop the call; neither throws.
        using (Datadog.Rum.StartView("before-init"))
        {
            Datadog.Rum.AddAction(RumActionType.Tap, "before-init");
            Datadog.Rum.AddError(new InvalidOperationException("before init"));
        }

        Datadog.Logger.Info("before init");

        using (Datadog.Tracer.StartSpan("before-init"))
        {
        }

        Assert(!Datadog.Rum.IsEnabled, "RUM reports itself as enabled before Initialize.");
        Assert(!Datadog.Logs.IsEnabled, "Logs reports itself as enabled before Initialize.");
    }

    private static void MalformedConfigurationIsRejected()
    {
        // The one place the façade does throw, and deliberately: this runs once at startup in code
        // you control, and an empty environment means every event is filed under the wrong one for
        // the life of the app with nothing to notice it by.
        Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration { ClientToken = "", Env = "e2e" }),
            "an empty client token");

        Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration { ClientToken = ClientToken, Env = "" }),
            "an empty environment");

        Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration
            {
                ClientToken = ClientToken,
                Env = "e2e",
                Rum = new RumOptions { ApplicationId = RumApplicationId, SessionSampleRate = 500 },
            }),
            "a sample rate above 100");

        Throws<ArgumentException>(
            () => Datadog.Initialize(new DatadogConfiguration
            {
                ClientToken = ClientToken,
                Env = "e2e",
                SessionReplay = new SessionReplayOptions(),
            }),
            "Session Replay without RUM");

        Assert(!Datadog.IsInitialized, "A rejected configuration left the SDK initialised.");
    }

    private static void InitializesEverything()
    {
        Datadog.Initialize(new DatadogConfiguration
        {
            ClientToken = ClientToken,
            Env = "e2e",
            Service = "datadognet-devicetests",
            Site = DatadogSite.Us1,
            TrackingConsent = TrackingConsent.Granted,
            Verbosity = DatadogVerbosity.Warn,
            BatchSize = BatchSize.Small,
            UploadFrequency = UploadFrequency.Frequent,
            BatchProcessingLevel = BatchProcessingLevel.High,
            Variant = "e2e",
            AdditionalConfiguration = new Dictionary<string, object?> { ["e2e"] = true },
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
                TrackAnonymousUser = true,
                VitalsUpdateFrequency = VitalsUpdateFrequency.Rare,
                LongTaskThreshold = TimeSpan.FromMilliseconds(250),
                CustomEndpoint = LocalEndpoint,
            },

            Logs = new LogsOptions { CustomEndpoint = LocalEndpoint },

            Trace = new TraceOptions
            {
                SampleRate = 100,
                NetworkInfoEnabled = true,
                BundleWithRumEnabled = true,
                GlobalTags = new Dictionary<string, string> { ["suite"] = "e2e" },
                HeaderTypes = [TracingHeaderType.Datadog, TracingHeaderType.TraceContext],
                CustomEndpoint = LocalEndpoint,
            },

            SessionReplay = new SessionReplayOptions
            {
                SampleRate = 100,
                TextAndInputPrivacy = TextAndInputPrivacy.MaskAll,
                ImagePrivacy = ImagePrivacy.MaskAll,
                TouchPrivacy = TouchPrivacy.Hide,
                StartRecordingImmediately = false,
                CustomEndpoint = LocalEndpoint,
            },
        });

        Assert(Datadog.IsInitialized, "Datadog.IsInitialized is false after Initialize returned.");
        Assert(Datadog.Rum.IsEnabled, "RUM was configured but reports itself as disabled.");
        Assert(Datadog.Tracer.IsEnabled, "Trace was configured but reports itself as disabled.");
        Assert(Datadog.SessionReplay.IsEnabled, "Session Replay was configured but reports itself as disabled.");

        // Initializing twice is ignored rather than treated as an error - a MAUI app on Android can
        // have its entry point run again when the process is recreated.
        Datadog.Initialize(new DatadogConfiguration { ClientToken = ClientToken, Env = "ignored" });
    }

    private static void SetsSdkLevelState()
    {
        Datadog.Verbosity = DatadogVerbosity.Debug;
        Report($"verbosity reads back as {Datadog.Verbosity}");

        Datadog.SetTrackingConsent(TrackingConsent.Pending);
        Datadog.SetTrackingConsent(TrackingConsent.Granted);

        Datadog.SetUser("user-123", "Ada Lovelace", "ada@example.com", new Dictionary<string, object?>
        {
            ["plan"] = "enterprise",
            ["seats"] = 42,
        });

        Datadog.AddUserExtraInfo(new Dictionary<string, object?> { ["role"] = "admin" });

        Datadog.SetAccount("acme-inc", "ACME Inc.", new Dictionary<string, object?> { ["tier"] = 1 });
        Datadog.AddAccountExtraInfo(new Dictionary<string, object?> { ["region"] = "eu" });

        Datadog.ClearAccount();
        Datadog.ClearUser();

        // Re-established, since everything after this is more useful attributed to someone.
        Datadog.SetUser("user-123");
    }

    private static void DrivesRum()
    {
        using (var view = Datadog.Rum.StartView("checkout", "Checkout"))
        {
            Assert(view.Key == "checkout", $"The view scope reported the key '{view.Key}'.");

            Datadog.Rum.AddAction(RumActionType.Tap, "pay", new Dictionary<string, object?>
            {
                ["cart.total"] = 42.50m,
            });

            Datadog.Rum.StartAction(RumActionType.Scroll, "cart");
            Datadog.Rum.StopAction(RumActionType.Scroll, "cart");

            Datadog.Rum.AddError(new InvalidOperationException("checkout failed"));
            Datadog.Rum.AddError("card declined", RumErrorSource.Network, stack: null);
        }

        // Stopping by hand and then disposing must not stop the view twice, which the SDK would
        // otherwise report as a second view lifecycle.
        var second = Datadog.Rum.StartView("checkout-2");
        Datadog.Rum.StopView("checkout-2");
        second.Dispose();
        second.Dispose();
    }

    private static void ReportsEveryAttributeType()
    {
        // Every type the two attribute converters claim to handle, in one call. They are separate
        // implementations - one produces Java objects, the other NSObjects - so this is where they
        // are held to the same contract.
        using var view = Datadog.Rum.StartView("attributes");

        Datadog.Rum.AddAction(RumActionType.Custom, "every-type", new Dictionary<string, object?>
        {
            ["string"] = "text",
            ["int"] = 42,
            ["long"] = 9_000_000_000L,
            ["short"] = (short)7,
            ["byte"] = (byte)3,
            ["float"] = 1.5f,
            ["double"] = 2.5d,
            ["decimal"] = 19.99m,
            ["bool"] = true,
            ["null"] = null,
            ["date"] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ["offset"] = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ["guid"] = Guid.Empty,
            ["enum"] = RumActionType.Tap,
            ["list"] = new[] { 1, 2, 3 },
            ["nested"] = new Dictionary<string, object?> { ["inner"] = "value" },
        });
    }

    private static void RejectsUnconvertibleAttribute()
    {
        // A value with no native representation must be rejected loudly rather than dropped: a
        // silently missing attribute is invisible until someone queries for it in Datadog months
        // later and finds nothing.
        Throws<ArgumentException>(
            () => Datadog.Rum.AddAction(
                RumActionType.Custom,
                "bad",
                new Dictionary<string, object?> { ["bad"] = new object() }),
            "an attribute value with no native representation");
    }

    private static void DrivesRumResources()
    {
        using var view = Datadog.Rum.StartView("resources");

        Datadog.Rum.StartResource("ok", RumHttpMethod.Get, "https://localhost/ok");
        Datadog.Rum.StopResource("ok", 200, RumResourceKind.Native, 1024);

        Datadog.Rum.StartResource("failed", RumHttpMethod.Post, "https://localhost/failed");
        Datadog.Rum.StopResourceWithError("failed", "server said no", 500);

        Datadog.Rum.StartResource("threw", RumHttpMethod.Put, "https://localhost/threw");
        Datadog.Rum.StopResourceWithError("threw", new TimeoutException("took too long"));

        Datadog.Rum.AddTiming("data-loaded");

        Datadog.Rum.AddFeatureFlagEvaluation("new-checkout", true);
        Datadog.Rum.AddFeatureFlagEvaluation("checkout-variant", "b");
    }

    private static void ManagesGlobalRumAttributes()
    {
        Datadog.Rum.AddAttribute("build.channel", "e2e");
        Datadog.Rum.AddAttributes(new Dictionary<string, object?>
        {
            ["build.number"] = 1,
            ["build.debug"] = true,
        });

        Datadog.Rum.RemoveAttribute("build.debug");
        Datadog.Rum.RemoveAttributes(["build.number"]);

        Datadog.Rum.Debug = true;
        Datadog.Rum.Debug = false;
    }

    private static async Task ReadsCurrentSessionId()
    {
        // Asynchronous on both platforms, through entirely different mechanisms: a block on iOS and
        // a kotlin.jvm.functions.Function1 on Android, which C# cannot express as a lambda at all.
        //
        // The timeout is generous because this is the only check whose result has to travel back
        // across the bridge on the SDK's own queue, and the app runs interpreted on a shared CI
        // runner. Ten seconds was enough locally and not on CI. It is a timeout rather than an
        // unbounded await so that a genuinely broken callback fails the run instead of hanging it
        // until the job's own limit.
        var sessionId = await Datadog.Rum.GetCurrentSessionIdAsync()
            .WaitAsync(TimeSpan.FromSeconds(60));

        Report($"session id: {sessionId ?? "(none)"}");

        Assert(
            sessionId is null || Guid.TryParse(sessionId, out _),
            $"The session id '{sessionId}' is neither null nor a GUID.");
    }

    private static void WritesEveryLogLevel()
    {
        Assert(Datadog.Logs.IsEnabled, "Logs was configured but reports itself as disabled.");

        var logger = Datadog.Logs.CreateLogger(new LoggerOptions
        {
            Name = "e2e",
            Service = "datadognet-devicetests",
            NetworkInfoEnabled = true,
            BundleWithRumEnabled = true,
            BundleWithTraceEnabled = true,
            RemoteSampleRate = 100,
            RemoteLogThreshold = DatadogLogLevel.Debug,
            PrintToConsole = true,
        });

        logger.Debug("debug");
        logger.Info("info");
        logger.Notice("notice");
        logger.Warn("warn");
        logger.Error("error");
        logger.Critical("critical");

        foreach (var level in Enum.GetValues<DatadogLogLevel>())
        {
            logger.Log(level, $"level {level}");
        }

        Datadog.Logger.Info("through the default logger");
    }

    private static void AttachesExceptionToLog()
    {
        try
        {
            throw new InvalidOperationException("something went wrong");
        }
        catch (Exception exception)
        {
            // The exception's type, message and stack must reach Datadog as error.kind,
            // error.message and error.stack rather than as three unrelated attributes - which is
            // why neither platform's raw API is used here: one wants a java.lang.Throwable and the
            // other an NSError.
            Datadog.Logger.Error("caught", exception, new Dictionary<string, object?>
            {
                ["order.id"] = "abc-123",
            });
        }
    }

    private static void ManagesLoggerAttributesAndTags()
    {
        var logger = Datadog.Logs.CreateLogger(new LoggerOptions { Name = "tagged" });

        logger.AddAttribute("session.kind", "e2e");
        logger.AddTag("suite", "device");
        logger.Info("tagged and attributed");

        logger.RemoveAttribute("session.kind");
        logger.RemoveTagsWithKey("suite");
        logger.Info("untagged");

        Datadog.Logs.AddAttribute("global.attribute", 1);
        Datadog.Logs.RemoveAttribute("global.attribute");
    }

    private static void DrivesTracing()
    {
        using (var span = Datadog.Tracer.StartSpan("checkout", tags: new Dictionary<string, object?>
        {
            ["cart.items"] = 3,
            ["cart.currency"] = "GBP",
        }))
        {
            span.SetTag("string.tag", "value");
            span.SetTag("number.tag", 1.5);
            span.SetTag("bool.tag", true);
            span.Log(new Dictionary<string, object?> { ["event"] = "checkout.started" });

            using (span.Activate())
            {
                Assert(
                    ReferenceEquals(Datadog.Tracer.ActiveSpan, span),
                    "The activated span is not the one the tracer reports as active.");

                using var child = Datadog.Tracer.StartSpan("payment");
                child.SetError(new TimeoutException("gateway timed out"));
            }

            Assert(Datadog.Tracer.ActiveSpan is null, "A span stayed active after its scope was left.");

            span.SetError("CheckoutError", "declined", stack: null);
        }

        // Finishing twice must be a no-op rather than a second span.
        var finished = Datadog.Tracer.StartSpan("finished-twice");
        finished.Finish();
        finished.Finish();
        finished.Dispose();
    }

    private static void ProducesTraceHeaders()
    {
        using var span = Datadog.Tracer.StartSpan("propagated");

        Report($"trace {span.TraceId} span {span.SpanId}");

        // The ids come from entirely different places: dd-sdk-android has toTraceId() on the span
        // context, and dd-sdk-ios has nothing at all - there they are parsed back out of an
        // injected Datadog-format header. Both must produce something.
        Assert(span.TraceId.Length > 0, "The span reported an empty trace id.");
        Assert(span.SpanId.Length > 0, "The span reported an empty span id.");

        var headers = Datadog.Tracer.Inject(span);

        Report($"headers: {string.Join(", ", headers.Keys)}");

        Assert(
            headers.ContainsKey("x-datadog-trace-id"),
            "Injection produced no x-datadog-trace-id header, so a trace would not continue into a " +
            "Datadog-instrumented backend.");

        Assert(
            headers.ContainsKey("traceparent"),
            "Injection produced no traceparent header, though TraceContext was configured.");
    }

    private static async Task ReportsHttpRequest()
    {
        using var view = Datadog.Rum.StartView("http");

        // Nothing listens on port 9, so the request fails - which is the interesting path: the
        // handler has to report the resource as failed and mark the span as an error without
        // swallowing the exception the caller is waiting for.
        using var client = new HttpClient(new DatadogHttpMessageHandler(new HttpClientHandler()))
        {
            Timeout = TimeSpan.FromSeconds(5),
        };

        var threw = false;

        try
        {
            await client.GetAsync("http://localhost:9/nothing");
        }
        catch (Exception exception)
        {
            threw = true;
            Report($"the request failed as expected: {exception.GetType().Name}");
        }

        Assert(threw, "The handler swallowed a failing request instead of rethrowing.");
    }

    private static void ControlsSessionReplay()
    {
        // StartRecordingImmediately was false, so this is the call that begins recording - which is
        // the shape an app wanting consent before recording needs.
        Datadog.SessionReplay.StartRecording();
        Datadog.SessionReplay.StopRecording();
        Datadog.SessionReplay.StartRecording();
    }

    private static void EnablesCrashReporting()
    {
        // After initialisation, never before: the crash reporter attaches to the RUM and Logs
        // features to file what it finds, and enabling it first is a silent no-op on both
        // platforms. Nothing here crashes on purpose - this only proves the package is present and
        // its native handler installs.
        CrashReporting.Enable();
        CrashReporting.Enable();
    }

    private static void StopsCleanly()
    {
        Datadog.Rum.StopSession();
        Datadog.ClearAllData();
        Datadog.Stop();

        Assert(!Datadog.IsInitialized, "Datadog.IsInitialized is true after Stop.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<TException>(Action action, string what)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{what} threw {exception.GetType().Name} rather than {typeof(TException).Name}.");
        }

        throw new InvalidOperationException($"{what} was accepted instead of throwing.");
    }
}
