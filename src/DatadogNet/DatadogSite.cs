namespace DatadogNet;

/// <summary>
/// The Datadog site your organisation's data is stored in.
/// </summary>
/// <remarks>
/// Getting this wrong is the single most common reason nothing ever appears in Datadog: the SDK
/// uploads happily to the wrong intake and reports no error. It is shown at the top of any
/// <c>app.datadoghq.*</c> page, and is part of the URL you log in at.
/// <para>
/// Only the sites both native SDKs declare are listed. dd-sdk-android 2.26.3 additionally has a
/// <c>STAGING</c> member, which is Datadog's own internal environment and has no dd-sdk-ios
/// counterpart; a façade that exposed it would compile on Android and throw on iOS.
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
}
