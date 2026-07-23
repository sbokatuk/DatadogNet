using System.Net.Http;

namespace DatadogNet;

/// <summary>
/// Reports every request that passes through it as a RUM resource, and continues the trace into
/// your own backend.
/// </summary>
/// <remarks>
/// This is how an <see cref="HttpClient"/>'s traffic gets into Datadog from a MAUI app, and it is
/// the one part of this façade with no native counterpart to delegate to. Neither SDK's automatic
/// network instrumentation can see a <see cref="HttpClient"/> call:
/// <list type="bullet">
/// <item>
/// <b>Android</b> — <c>DatadogInterceptor</c> is an OkHttp interceptor, and <c>HttpClient</c> does
/// not route through OkHttp unless the app configures <c>AndroidMessageHandler</c> itself.
/// </item>
/// <item>
/// <b>iOS</b> — <c>DDURLSessionInstrumentation</c> hooks an <c>NSURLSession</c> delegate class, and
/// <c>NSUrlSessionHandler</c> owns its delegate rather than exposing it.
/// </item>
/// </list>
/// Doing it in the managed pipeline instead works identically on both, and works for
/// <c>SocketsHttpHandler</c> too — which is what a MAUI app gets when it opts out of the native
/// stack.
/// <code>
/// services.AddHttpClient ("api", client => client.BaseAddress = new Uri ("https://api.example.com"))
///         .AddHttpMessageHandler (() => new DatadogHttpMessageHandler ());
/// </code>
/// Or, without dependency injection:
/// <code>
/// var http = new HttpClient (new DatadogHttpMessageHandler ());
/// </code>
/// <para>
/// Tracing headers are attached only to hosts listed in
/// <see cref="DatadogConfiguration.FirstPartyHosts"/>. Everything else is still reported as a
/// resource — you want to see how slow a third-party API is — but is never given a trace id.
/// </para>
/// </remarks>
public sealed class DatadogHttpMessageHandler : DelegatingHandler
{
    /// <summary>
    /// Creates a handler over the platform's default inner handler.
    /// </summary>
    /// <remarks>
    /// For <c>IHttpClientFactory</c>, which supplies the inner handler
    /// itself. Constructing one of these directly and handing it to
    /// <see cref="HttpClient(HttpMessageHandler)"/> leaves the inner handler unset and throws on
    /// first use — use <see cref="DatadogHttpMessageHandler(HttpMessageHandler)"/> there.
    /// </remarks>
    public DatadogHttpMessageHandler()
    {
    }

    /// <summary>Creates a handler over a specific inner handler.</summary>
    /// <param name="innerHandler">The handler that actually performs the request.</param>
    public DatadogHttpMessageHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    /// <summary>
    /// Whether to report requests as RUM resources. Defaults to <see langword="true"/>.
    /// </summary>
    public bool TrackResources { get; init; } = true;

    /// <summary>
    /// Whether to wrap each request in a span and propagate it to first-party hosts. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool TrackTraces { get; init; } = true;

    /// <summary>
    /// What kind of resource requests are reported as. Defaults to
    /// <see cref="RumResourceKind.Native"/>.
    /// </summary>
    public RumResourceKind ResourceKind { get; init; } = RumResourceKind.Native;

    /// <summary>
    /// Names the span for a request. Defaults to <c>http.request</c> for every request.
    /// </summary>
    /// <remarks>
    /// APM groups by operation name, so it must be low cardinality — a URL, or anything containing
    /// an id, makes the trace list useless. Override it to distinguish broad classes of call
    /// (<c>api.orders</c>, <c>api.catalog</c>), never individual endpoints.
    /// </remarks>
    public Func<HttpRequestMessage, string>? OperationName { get; init; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = request.RequestUri?.ToString() ?? string.Empty;
        var method = ToRumMethod(request.Method);

        var span = StartSpanFor(request);
        var resourceKey = TrackResources && Datadog.Rum.IsEnabled
            ? Guid.NewGuid().ToString("N")
            : null;

        if (resourceKey is not null)
        {
            // The trace and span ids go on as Datadog's own reserved attribute names, which is what
            // makes the RUM resource and the APM trace link to each other in the product. Nothing
            // else produces that link: the two are separate intakes, correlated only by these.
            var attributes = span is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["_dd.trace_id"] = span.TraceId,
                    ["_dd.span_id"] = span.SpanId,
                };

            Datadog.Rum.StartResource(resourceKey, method, url, attributes);
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (resourceKey is not null)
            {
                Datadog.Rum.StopResource(
                    resourceKey,
                    (int)response.StatusCode,
                    ResourceKind,
                    // Content-Length rather than reading the body: the response has not been
                    // consumed yet, and buffering it here to measure it would change the streaming
                    // behaviour of every caller. Null when the server chunked the response, which
                    // Datadog renders as "unknown" rather than as zero.
                    response.Content?.Headers.ContentLength);
            }

            span?.SetTag("http.status_code", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                span?.SetError(
                    "HttpRequestException",
                    $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd(),
                    stack: null);
            }

            return response;
        }
        catch (Exception exception)
        {
            if (resourceKey is not null)
            {
                Datadog.Rum.StopResourceWithError(resourceKey, exception);
            }

            span?.SetError(exception);
            throw;
        }
        finally
        {
            span?.Dispose();
        }
    }

    private IDatadogSpan? StartSpanFor(HttpRequestMessage request)
    {
        if (!TrackTraces || !Datadog.Tracer.IsEnabled)
        {
            return null;
        }

        var span = Datadog.Tracer.StartSpan(
            OperationName?.Invoke(request) ?? "http.request",
            tags: new Dictionary<string, object?>
            {
                ["http.method"] = request.Method.Method,
                ["http.url"] = request.RequestUri?.ToString(),
                // resource.name is what APM shows as the operation's detail line, distinct from the
                // operation name it groups by.
                ["resource.name"] = $"{request.Method.Method} {request.RequestUri?.AbsolutePath}",
                ["span.kind"] = "client",
            });

        if (!IsFirstParty(request.RequestUri))
        {
            // Still traced locally, so the call shows up in the app's own trace and on the RUM
            // resource - but no headers, because a Datadog trace id on a third-party request leaks
            // internal topology to someone else's logs.
            return span;
        }

        foreach (var header in Datadog.Tracer.Inject(span))
        {
            // TryAddWithoutValidation, not Add: a caller may already have set traceparent itself,
            // and throwing here would fail a request over instrumentation.
            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return span;
    }

    /// <summary>
    /// Whether a URL's host is one of <see cref="DatadogConfiguration.FirstPartyHosts"/>.
    /// </summary>
    /// <remarks>
    /// Suffix matching on a label boundary, which is what both native SDKs do: <c>example.com</c>
    /// covers <c>api.example.com</c> but not <c>notexample.com</c>. The boundary check is the whole
    /// point — a plain <c>EndsWith</c> would send your trace ids to anyone who registered a domain
    /// ending in your own.
    /// </remarks>
    private static bool IsFirstParty(Uri? uri)
    {
        if (uri is null || Datadog.Configuration?.FirstPartyHosts is not { Count: > 0 } hosts)
        {
            return false;
        }

        var host = uri.Host;

        foreach (var candidate in hosts.Keys)
        {
            if (host.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (host.Length > candidate.Length
                && host.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)
                && host[host.Length - candidate.Length - 1] == '.')
            {
                return true;
            }
        }

        return false;
    }

    private static RumHttpMethod ToRumMethod(HttpMethod method) => method.Method.ToUpperInvariant() switch
    {
        "GET" => RumHttpMethod.Get,
        "POST" => RumHttpMethod.Post,
        "PUT" => RumHttpMethod.Put,
        "PATCH" => RumHttpMethod.Patch,
        "DELETE" => RumHttpMethod.Delete,
        "HEAD" => RumHttpMethod.Head,
        "OPTIONS" => RumHttpMethod.Options,
        "CONNECT" => RumHttpMethod.Connect,
        "TRACE" => RumHttpMethod.Trace,
        // Both SDKs take the method as a closed enum, so there is nowhere to put a custom verb.
        // GET is the least misleading of the nine, and the request itself still carries the real
        // one in its attributes.
        _ => RumHttpMethod.Get,
    };
}
