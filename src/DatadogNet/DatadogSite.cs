namespace DatadogNet;

/// <summary>
/// The Datadog site your organisation's data is stored in.
/// </summary>
/// <remarks>
/// Getting this wrong is the single most common reason nothing ever appears in Datadog: the SDK
/// uploads happily to the wrong intake and reports no error. It is shown at the top of any
/// <c>app.datadoghq.*</c> page, and is part of the URL you log in at.
/// <para>
/// Only the sites both native SDKs declare are listed. dd-sdk-android 3.12.1 additionally has
/// <c>STAGING</c> — Datadog's own internal environment — and <c>UK1</c>, neither of which
/// dd-sdk-ios 3.14.0 has; a façade that exposed either would compile on Android and throw on iOS.
/// </para>
/// </remarks>
public enum DatadogSite
{
    /// <summary>US1 — <c>app.datadoghq.com</c>. The default.</summary>
    Us1,

    /// <summary>US3 — <c>us3.datadoghq.com</c>.</summary>
    Us3,

    /// <summary>US5 — <c>us5.datadoghq.com</c>.</summary>
    Us5,

    /// <summary>EU1 — <c>app.datadoghq.eu</c>.</summary>
    Eu1,

    /// <summary>AP1 — <c>ap1.datadoghq.com</c>.</summary>
    Ap1,

    /// <summary>AP2 — <c>ap2.datadoghq.com</c>.</summary>
    Ap2,

    /// <summary>US1-FED — <c>app.ddog-gov.com</c>, the FedRAMP environment.</summary>
    Us1Fed,

    /// <summary>
    /// US2-FED — the second FedRAMP environment.
    /// </summary>
    /// <remarks>New in the 3.x line: dd-sdk-ios 2.30.2 had no <c>us2_fed</c>, so it could not be
    /// offered before both platforms declared it.</remarks>
    Us2Fed,
}
