namespace DatadogNet;

/// <summary>
/// The Datadog SDK: initialisation, consent, identity, and the way in to every feature.
/// </summary>
/// <remarks>
/// One cross-platform surface over
/// <a href="https://github.com/sbokatuk/DatadogNet.iOS">DatadogNet.iOS</a> and
/// <a href="https://github.com/sbokatuk/DatadogNet.Android">DatadogNet.Android</a>, so a MAUI app
/// writes its instrumentation once.
/// <code>
/// Datadog.Initialize (new DatadogConfiguration {
///     ClientToken     = "…",
///     Env             = "production",
///     Service         = "my-app",
///     TrackingConsent = TrackingConsent.Granted,
///     Rum             = new RumOptions { ApplicationId = "…" },
///     Logs            = new LogsOptions (),
/// });
///
/// using (Datadog.Rum.StartView ("checkout", "Checkout")) {
///     Datadog.Logger.Info ("checkout opened");
/// }
/// </code>
/// <para>
/// <b>Nothing here throws because Datadog is unavailable.</b> On a Windows head — or in a plain
/// unit-test process — <see cref="IsSupported"/> is <see langword="false"/> and every call is a
/// no-op, so shared MAUI code needs no platform conditionals. Calls made before
/// <see cref="Initialize"/> are dropped the same way, though a logger obtained early starts
/// delivering once the SDK is up. Instrumentation that crashes the app it is measuring is worse
/// than instrumentation that is missing.
/// </para>
/// </remarks>
public static partial class Datadog
{
    private static readonly Lazy<IRumMonitor> LazyRum = new(CreateRum, isThreadSafe: true);
    private static readonly Lazy<IDatadogLogs> LazyLogs = new(CreateLogs, isThreadSafe: true);
    private static readonly Lazy<IDatadogTracer> LazyTracer = new(CreateTracer, isThreadSafe: true);
    private static readonly Lazy<ISessionReplay> LazySessionReplay = new(CreateSessionReplay, isThreadSafe: true);

    // Not readonly: Stop() swaps in a fresh lazy. A logger materialised in one SDK epoch holds the
    // old epoch's native and would be dead in the next; the other lazies hold adapters that resolve
    // their native per call, so they carry across epochs and can stay frozen.
    private static Lazy<IDatadogLogger> lazyLogger = new(() => Logs.CreateLogger(), isThreadSafe: true);

    private static readonly object InitializeGate = new();

    /// <summary>
    /// Whether this platform has a Datadog implementation at all.
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> on Android, iOS and Mac Catalyst, <see langword="false"/> everywhere
    /// else — a Windows MAUI head, or a unit test running on the plain
    /// <c>net9.0</c>/<c>net10.0</c> assembly. Worth branching on only when the alternative is doing
    /// real work whose result would be thrown away; the API itself needs no guarding.
    /// </remarks>
    public static bool IsSupported => PlatformIsSupported();

    /// <summary>Whether <see cref="Initialize"/> has run and the SDK is live.</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>Reports RUM views, actions, resources and errors.</summary>
    public static IRumMonitor Rum => LazyRum.Value;

    /// <summary>Creates loggers.</summary>
    public static IDatadogLogs Logs => LazyLogs.Value;

    /// <summary>
    /// A ready-made logger, created on first use with the SDK's default settings.
    /// </summary>
    /// <remarks>
    /// For an app that wants the single obvious logger and no ceremony. Use
    /// <see cref="IDatadogLogs.CreateLogger"/> when you want a name, a service, or a different
    /// sampling rate. Safe to touch before <see cref="Initialize"/>: it starts delivering at the
    /// first write after the SDK is up (see <see cref="IDatadogLogger"/> for the exact contract).
    /// </remarks>
    public static IDatadogLogger Logger => lazyLogger.Value;

    /// <summary>Starts spans.</summary>
    public static IDatadogTracer Tracer => LazyTracer.Value;

    /// <summary>Starts and stops Session Replay recording.</summary>
    public static ISessionReplay SessionReplay => LazySessionReplay.Value;

    /// <summary>
    /// Initialises the SDK and enables the features the configuration asks for.
    /// </summary>
    /// <param name="configuration">What to start, and how.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="DatadogConfiguration.ClientToken"/> or <see cref="DatadogConfiguration.Env"/> is
    /// empty, or a sample rate is outside 0–100.
    /// </exception>
    /// <remarks>
    /// Call once, as early as the app can — from <c>MauiProgram.CreateMauiApp</c> before the
    /// builder runs, or earlier still from the platform application class. Crash reporting only
    /// covers what happens after the SDK is up, and startup crashes are the ones worth catching.
    /// <para>
    /// Calling it a second time is ignored rather than treated as an error, because a MAUI app can
    /// have its entry point run twice on Android when the process is recreated. The second call
    /// does <b>not</b> reconfigure anything.
    /// </para>
    /// <para>
    /// A malformed configuration throws, unlike everything else on this type. This runs once, at
    /// startup, in code you control — an environment of <c>""</c> means every event is filed under
    /// the wrong environment for the life of the app, and there is no later point at which that
    /// becomes visible.
    /// </para>
    /// </remarks>
    public static void Initialize(DatadogConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Validate(configuration);

        lock (InitializeGate)
        {
            if (IsInitialized)
            {
                return;
            }

            PlatformInitialize(configuration);
            Configuration = configuration;
            IsInitialized = true;
        }
    }

    /// <summary>
    /// The configuration the SDK was initialised with, or <see langword="null"/> before
    /// <see cref="Initialize"/>.
    /// </summary>
    /// <remarks>
    /// Internal, and read by <see cref="DatadogHttpMessageHandler"/> for
    /// <see cref="DatadogConfiguration.FirstPartyHosts"/>. Both native SDKs keep first-party hosts
    /// for their own network instrumentation and expose no way to read them back, and a handler
    /// that had to be told the host list separately would be one more thing to keep in step with
    /// the configuration.
    /// </remarks>
    internal static DatadogConfiguration? Configuration { get; private set; }

    /// <summary>
    /// Changes whether the SDK may collect and upload.
    /// </summary>
    /// <remarks>
    /// Moving from <see cref="TrackingConsent.Pending"/> to <see cref="TrackingConsent.Granted"/>
    /// uploads what was held; moving to <see cref="TrackingConsent.NotGranted"/> discards it.
    /// </remarks>
    public static void SetTrackingConsent(TrackingConsent consent) => PlatformSetTrackingConsent(consent);

    /// <summary>How loudly the SDK reports its own problems.</summary>
    public static DatadogVerbosity Verbosity
    {
        get => PlatformGetVerbosity();
        set => PlatformSetVerbosity(value);
    }

    /// <summary>
    /// Identifies the current user, so their sessions can be found and their events grouped.
    /// </summary>
    /// <param name="id">A stable identifier. Not necessarily a display name.</param>
    /// <param name="name">A display name.</param>
    /// <param name="email">An email address.</param>
    /// <param name="extraInfo">Anything else worth querying on — a plan, a role, a tenant.</param>
    /// <remarks>
    /// Applies to every feature at once: RUM sessions, logs and spans all carry it. Call it as soon
    /// as you know who the user is, and <see cref="ClearUser"/> when they sign out.
    /// </remarks>
    public static void SetUser(
        string id,
        string? name = null,
        string? email = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        PlatformSetUser(id, name, email, extraInfo);
    }

    /// <summary>Adds to the current user's extra information without replacing it.</summary>
    public static void AddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
    {
        ArgumentNullException.ThrowIfNull(extraInfo);
        PlatformAddUserExtraInfo(extraInfo);
    }

    /// <summary>Forgets the current user. Call on sign-out.</summary>
    public static void ClearUser() => PlatformClearUser();

    /// <summary>
    /// Identifies the account, organisation or tenant the user is acting within.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SetUser"/>, and the right place for the tenant in a B2B app: one
    /// user can belong to several accounts, and Datadog facets them independently.
    /// </remarks>
    public static void SetAccount(
        string id,
        string? name = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        PlatformSetAccount(id, name, extraInfo);
    }

    /// <summary>Adds to the current account's extra information without replacing it.</summary>
    public static void AddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo)
    {
        ArgumentNullException.ThrowIfNull(extraInfo);
        PlatformAddAccountExtraInfo(extraInfo);
    }

    /// <summary>Forgets the current account.</summary>
    public static void ClearAccount() => PlatformClearAccount();

    /// <summary>
    /// Deletes every event still waiting on disk.
    /// </summary>
    /// <remarks>
    /// For a "delete my data" request. Does not stop collection — use
    /// <see cref="SetTrackingConsent"/> with <see cref="TrackingConsent.NotGranted"/> for that.
    /// </remarks>
    public static void ClearAllData() => PlatformClearAllData();

    /// <summary>
    /// Stops the SDK: collection ends and every feature is disabled.
    /// </summary>
    /// <remarks>
    /// After this, <see cref="Initialize"/> can be called again with a different configuration.
    /// Rarely what you want — <see cref="SetTrackingConsent"/> is the answer to "stop collecting",
    /// and it keeps the option of resuming.
    /// </remarks>
    public static void Stop()
    {
        lock (InitializeGate)
        {
            PlatformStop();
            Configuration = null;
            IsInitialized = false;

            // The shared logger belongs to the epoch that made it; the next access after a
            // re-Initialize must build against the new SDK instance, not write into the stopped
            // one. Loggers the app created itself through Logs.CreateLogger are the app's to
            // recreate — this type cannot reach them.
            lazyLogger = new(() => Logs.CreateLogger(), isThreadSafe: true);
        }
    }

    private static void Validate(DatadogConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ClientToken))
        {
            throw new ArgumentException(
                "A client token is required. Take it from Organization Settings -> Client Tokens; " +
                "it is not the same as an API key.",
                nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.Env))
        {
            throw new ArgumentException(
                "An environment is required - 'production', 'staging', and so on. Both native SDKs " +
                "reject an empty one, and every event carries it as a tag.",
                nameof(configuration));
        }

        if (configuration.Rum is { } rum)
        {
            if (string.IsNullOrWhiteSpace(rum.ApplicationId))
            {
                throw new ArgumentException(
                    "RumOptions.ApplicationId is required. It comes from UX Monitoring -> RUM " +
                    "Applications, and is not the client token.",
                    nameof(configuration));
            }

            ValidateRate(rum.SessionSampleRate, "RumOptions.SessionSampleRate");
            ValidateRate(rum.TelemetrySampleRate, "RumOptions.TelemetrySampleRate");
        }

        if (configuration.Trace is { } trace)
        {
            ValidateRate(trace.SampleRate, "TraceOptions.SampleRate");
        }

        if (configuration.SessionReplay is { } replay)
        {
            ValidateRate(replay.SampleRate, "SessionReplayOptions.SampleRate");

            if (configuration.Rum is null)
            {
                throw new ArgumentException(
                    "Session Replay requires RUM: a replay is attached to a RUM session, so " +
                    "enabling it without RUM records nothing. Set DatadogConfiguration.Rum.",
                    nameof(configuration));
            }
        }

        static void ValidateRate(float rate, string name)
        {
            if (rate is < 0 or > 100 || float.IsNaN(rate))
            {
                throw new ArgumentException($"{name} must be between 0 and 100, but was {rate}.");
            }
        }
    }

    private static partial bool PlatformIsSupported();

    private static partial void PlatformInitialize(DatadogConfiguration configuration);

    private static partial void PlatformSetTrackingConsent(TrackingConsent consent);

    private static partial DatadogVerbosity PlatformGetVerbosity();

    private static partial void PlatformSetVerbosity(DatadogVerbosity verbosity);

    private static partial void PlatformSetUser(
        string id,
        string? name,
        string? email,
        IReadOnlyDictionary<string, object?>? extraInfo);

    private static partial void PlatformAddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo);

    private static partial void PlatformClearUser();

    private static partial void PlatformSetAccount(
        string id,
        string? name,
        IReadOnlyDictionary<string, object?>? extraInfo);

    private static partial void PlatformAddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo);

    private static partial void PlatformClearAccount();

    private static partial void PlatformClearAllData();

    private static partial void PlatformStop();

    private static partial IRumMonitor CreateRum();

    private static partial IDatadogLogs CreateLogs();

    private static partial IDatadogTracer CreateTracer();

    private static partial ISessionReplay CreateSessionReplay();
}
