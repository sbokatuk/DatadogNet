namespace DatadogNet;

/// <summary>
/// The SDK's lifecycle, identity and consent surface as a service: everything
/// <see cref="Datadog"/> does, injectable and therefore substitutable.
/// </summary>
/// <remarks>
/// The feature surfaces — <see cref="IRumMonitor"/>, <see cref="IDatadogLogs"/> and the rest —
/// have been injectable all along, which covers code that <i>reports</i>. This interface covers
/// code that <i>drives the SDK</i>: "we call <see cref="SetUser"/> on sign-in", "accepting the
/// consent banner flips <see cref="SetTrackingConsent"/>", "the delete-my-data flow calls
/// <see cref="ClearAllData"/>" — behaviour worth a unit test, and untestable against a static.
/// <para>
/// <see cref="DatadogSdk.Instance"/> is the implementation that delegates to the static façade,
/// and what the dependency-injection package registers. Everything on it keeps the static's
/// contract: no member throws because Datadog is unavailable, and only
/// <see cref="Initialize"/> validates.
/// </para>
/// </remarks>
public interface IDatadogSdk
{
    /// <inheritdoc cref="Datadog.IsSupported"/>
    bool IsSupported { get; }

    /// <inheritdoc cref="Datadog.IsInitialized"/>
    bool IsInitialized { get; }

    /// <inheritdoc cref="Datadog.Rum"/>
    IRumMonitor Rum { get; }

    /// <inheritdoc cref="Datadog.Logs"/>
    IDatadogLogs Logs { get; }

    /// <inheritdoc cref="Datadog.Logger"/>
    IDatadogLogger Logger { get; }

    /// <inheritdoc cref="Datadog.Tracer"/>
    IDatadogTracer Tracer { get; }

    /// <inheritdoc cref="Datadog.SessionReplay"/>
    ISessionReplay SessionReplay { get; }

    /// <inheritdoc cref="Datadog.Verbosity"/>
    DatadogVerbosity Verbosity { get; set; }

    /// <inheritdoc cref="Datadog.Initialize"/>
    void Initialize(DatadogConfiguration configuration);

    /// <inheritdoc cref="Datadog.SetTrackingConsent"/>
    void SetTrackingConsent(TrackingConsent consent);

    /// <inheritdoc cref="Datadog.SetUser"/>
    void SetUser(
        string id,
        string? name = null,
        string? email = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null);

    /// <inheritdoc cref="Datadog.AddUserExtraInfo"/>
    void AddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo);

    /// <inheritdoc cref="Datadog.ClearUser"/>
    void ClearUser();

    /// <inheritdoc cref="Datadog.SetAccount"/>
    void SetAccount(
        string id,
        string? name = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null);

    /// <inheritdoc cref="Datadog.AddAccountExtraInfo"/>
    void AddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo);

    /// <inheritdoc cref="Datadog.ClearAccount"/>
    void ClearAccount();

    /// <inheritdoc cref="Datadog.ClearAllData"/>
    void ClearAllData();

    /// <inheritdoc cref="Datadog.Stop"/>
    void Stop();
}

/// <summary>
/// The <see cref="IDatadogSdk"/> that is the real SDK: every member delegates to
/// <see cref="Datadog"/>.
/// </summary>
/// <remarks>
/// Stateless, hence the single <see cref="Instance"/>. Constructible anyway, for a host that
/// wants its own reference rather than a shared one — the two behave identically because the
/// state lives in the static façade either way.
/// </remarks>
public sealed class DatadogSdk : IDatadogSdk
{
    /// <summary>The shared instance.</summary>
    public static DatadogSdk Instance { get; } = new();

    /// <inheritdoc />
    public bool IsSupported => Datadog.IsSupported;

    /// <inheritdoc />
    public bool IsInitialized => Datadog.IsInitialized;

    /// <inheritdoc />
    public IRumMonitor Rum => Datadog.Rum;

    /// <inheritdoc />
    public IDatadogLogs Logs => Datadog.Logs;

    /// <inheritdoc />
    public IDatadogLogger Logger => Datadog.Logger;

    /// <inheritdoc />
    public IDatadogTracer Tracer => Datadog.Tracer;

    /// <inheritdoc />
    public ISessionReplay SessionReplay => Datadog.SessionReplay;

    /// <inheritdoc />
    public DatadogVerbosity Verbosity
    {
        get => Datadog.Verbosity;
        set => Datadog.Verbosity = value;
    }

    /// <inheritdoc />
    public void Initialize(DatadogConfiguration configuration) => Datadog.Initialize(configuration);

    /// <inheritdoc />
    public void SetTrackingConsent(TrackingConsent consent) => Datadog.SetTrackingConsent(consent);

    /// <inheritdoc />
    public void SetUser(
        string id,
        string? name = null,
        string? email = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null) =>
        Datadog.SetUser(id, name, email, extraInfo);

    /// <inheritdoc />
    public void AddUserExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        Datadog.AddUserExtraInfo(extraInfo);

    /// <inheritdoc />
    public void ClearUser() => Datadog.ClearUser();

    /// <inheritdoc />
    public void SetAccount(
        string id,
        string? name = null,
        IReadOnlyDictionary<string, object?>? extraInfo = null) =>
        Datadog.SetAccount(id, name, extraInfo);

    /// <inheritdoc />
    public void AddAccountExtraInfo(IReadOnlyDictionary<string, object?> extraInfo) =>
        Datadog.AddAccountExtraInfo(extraInfo);

    /// <inheritdoc />
    public void ClearAccount() => Datadog.ClearAccount();

    /// <inheritdoc />
    public void ClearAllData() => Datadog.ClearAllData();

    /// <inheritdoc />
    public void Stop() => Datadog.Stop();
}
