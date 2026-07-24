using Microsoft.Extensions.DependencyInjection;

namespace DatadogNet;

/// <summary>
/// Adds Datadog instrumentation to a named or typed <c>HttpClient</c>.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Reports this client's requests as RUM resources, and continues the trace into first-party
    /// hosts.
    /// </summary>
    /// <param name="builder">The client builder from <c>AddHttpClient</c>.</param>
    /// <param name="configure">
    /// Adjusts the handler — to turn tracing off for one client, say, or to name its spans.
    /// </param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// <code>
    /// builder.Services
    ///     .AddHttpClient&lt;OrderService&gt; (client =&gt; client.BaseAddress = new Uri ("https://api.example.com"))
    ///     .AddDatadogTracking ();
    /// </code>
    /// Added per client rather than globally, deliberately: an app usually has one client for its
    /// own backend and others for third parties, and only the first should be given trace headers.
    /// Which hosts those are is still decided by
    /// <see cref="DatadogConfiguration.FirstPartyHosts"/> — this only decides which clients are
    /// looked at.
    /// </remarks>
    public static IHttpClientBuilder AddDatadogTracking(
        this IHttpClientBuilder builder,
        Action<DatadogHttpMessageHandlerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new DatadogHttpMessageHandlerOptions();
        configure?.Invoke(options);

        return builder.AddHttpMessageHandler(() => new DatadogHttpMessageHandler
        {
            TrackResources = options.TrackResources,
            TrackTraces = options.TrackTraces,
            ResourceKind = options.ResourceKind,
            OperationName = options.OperationName,
        });
    }
}

/// <summary>
/// How <see cref="HttpClientBuilderExtensions.AddDatadogTracking"/> configures its handler.
/// </summary>
/// <remarks>
/// A mutable mirror of <see cref="DatadogHttpMessageHandler"/>'s init-only properties, because the
/// handler is constructed per client by the factory rather than by the caller.
/// </remarks>
public sealed class DatadogHttpMessageHandlerOptions
{
    /// <inheritdoc cref="DatadogHttpMessageHandler.TrackResources"/>
    public bool TrackResources { get; set; } = true;

    /// <inheritdoc cref="DatadogHttpMessageHandler.TrackTraces"/>
    public bool TrackTraces { get; set; } = true;

    /// <inheritdoc cref="DatadogHttpMessageHandler.ResourceKind"/>
    public RumResourceKind ResourceKind { get; set; } = RumResourceKind.Native;

    /// <inheritdoc cref="DatadogHttpMessageHandler.OperationName"/>
    public Func<System.Net.Http.HttpRequestMessage, string>? OperationName { get; set; }
}
