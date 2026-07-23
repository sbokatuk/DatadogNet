using System.Text;
using DatadogNet;
using Microsoft.Extensions.Logging;

namespace DatadogNet.Maui.Sample;

/// <summary>
/// One button per thing the façade can do, so the sample is also a checklist.
/// </summary>
/// <remarks>
/// Everything here is the cross-platform API. There is not a single <c>#if ANDROID</c> or
/// <c>#if IOS</c> in this file, which is the point of the repository.
/// </remarks>
public partial class MainPage : ContentPage
{
    private readonly IRumMonitor rum;
    private readonly IDatadogLogger logger;
    private readonly IDatadogTracer tracer;
    private readonly ISessionReplay sessionReplay;
    private readonly ILogger<MainPage> frameworkLogger;
    private readonly IHttpClientFactory httpClientFactory;

    private readonly StringBuilder output = new();

    /// <remarks>
    /// The Datadog pieces are injected rather than reached through the static <c>Datadog</c> class.
    /// Both work; this one lets a test hand the page a substitute. <c>UseDatadog</c> registers all
    /// five as singletons.
    /// </remarks>
    public MainPage(
        IRumMonitor rum,
        IDatadogLogger logger,
        IDatadogTracer tracer,
        ISessionReplay sessionReplay,
        ILogger<MainPage> frameworkLogger,
        IHttpClientFactory httpClientFactory)
    {
        this.rum = rum;
        this.logger = logger;
        this.tracer = tracer;
        this.sessionReplay = sessionReplay;
        this.frameworkLogger = frameworkLogger;
        this.httpClientFactory = httpClientFactory;

        InitializeComponent();

        StatusLabel.Text = MauiProgram.IsConfigured
            ? $"Reporting to Datadog. Supported: {Datadog.IsSupported}."
            : "Placeholder credentials, so nothing is uploaded — but every call below runs for real "
              + $"against the native SDK. Supported: {Datadog.IsSupported}.";
    }

    private void OnReportAction(object? sender, EventArgs e)
    {
        rum.AddAction(RumActionType.Tap, "report-action", new Dictionary<string, object?>
        {
            ["cart.total"] = 42.50m,
            ["cart.items"] = 3,
            ["checkout.step"] = "payment",
        });

        Write("Reported a tap action with three attributes.");
    }

    private void OnReportError(object? sender, EventArgs e)
    {
        try
        {
            throw new InvalidOperationException("the payment gateway said no");
        }
        catch (Exception exception)
        {
            // The exception's type, message and stack reach Datadog as the error's kind, message and
            // stack — so errors group by where they were thrown rather than by message text.
            rum.AddError(exception);
            Write($"Reported {exception.GetType().Name} as a RUM error.");
        }
    }

    private void OnReportView(object? sender, EventArgs e)
    {
        // A view that is not a page: a modal step, a wizard stage, anything the user would call a
        // screen. Scoped to a using block so an early return or an exception cannot leave it open —
        // a view left open goes on collecting every later action and error in the session.
        using (var view = rum.StartView("checkout-flow", "Checkout"))
        {
            // View-scoped attributes, new in the 3.x line. These land on the view *and* on
            // everything reported inside it, so the action below carries cart.id and cart.items
            // without either being passed along - and both disappear when the view closes, unlike
            // rum.AddAttribute, which would attach them to the rest of the session.
            view.AddAttributes(new Dictionary<string, object?>
            {
                ["cart.id"] = Guid.NewGuid().ToString("N"),
                ["cart.items"] = 3,
            });

            rum.AddTiming("cart-loaded");
            rum.AddAction(RumActionType.Custom, "inside-custom-view");

            // When the screen is actually usable, which is rarely when it appeared.
            view.AddLoadingTime();
        }

        Write("Opened and closed a custom RUM view with scoped attributes and a loading time.");
    }

    private void OnReportFeatureFlag(object? sender, EventArgs e)
    {
        rum.AddFeatureFlagEvaluation("new-checkout", true);
        rum.AddFeatureFlagEvaluation("checkout-variant", "b");

        Write("Recorded two feature-flag evaluations, so RUM events can be split by variant.");
    }

    private async void OnShowSessionId(object? sender, EventArgs e)
    {
        // Worth attaching to a support ticket: it is what turns "the app was slow" into a session
        // you can watch.
        var sessionId = await rum.GetCurrentSessionIdAsync();

        Write($"Session id: {sessionId ?? "(none — RUM is off, or this session was sampled out)"}");
    }

    private void OnWriteLogs(object? sender, EventArgs e)
    {
        logger.Debug("debug");
        logger.Info("info");
        logger.Notice("notice");
        logger.Warn("warn");
        logger.Error("error");
        logger.Critical("critical");

        Write("Wrote one log at each of Datadog's six levels.");
    }

    private void OnLogException(object? sender, EventArgs e)
    {
        try
        {
            _ = int.Parse("not a number");
        }
        catch (Exception exception)
        {
            logger.Error("could not parse the response", exception, new Dictionary<string, object?>
            {
                ["order.id"] = "abc-123",
            });

            Write("Logged an exception; it renders as an error rather than three loose attributes.");
        }
    }

    private void OnWriteThroughILogger(object? sender, EventArgs e)
    {
        // No Datadog API in sight. The provider UseDatadog registered forwards this, tagged with
        // this class as its logger name and correlated with the RUM view it was written in — so an
        // app that already logs through ILogger<T> needs no call-site changes at all.
        frameworkLogger.LogInformation("Wrote through ILogger<MainPage> with order {OrderId}", "abc-123");

        Write("Wrote through ILogger<MainPage>; it arrives with logger.name = this class.");
    }

    private void OnRunTracedOperation(object? sender, EventArgs e)
    {
        using (var span = tracer.StartSpan("checkout", tags: new Dictionary<string, object?>
        {
            ["cart.items"] = 3,
        }))
        {
            span.SetTag("cart.currency", "GBP");

            // Spans started while this one is active become its children. Activation follows the
            // native SDKs' own thread-local scope managers, so it does not flow across an await.
            using (span.Activate())
            {
                using var child = tracer.StartSpan("price-lookup");
                child.SetTag("cache.hit", false);
            }

            Write($"Traced an operation. trace {span.TraceId}, span {span.SpanId}.");
        }
    }

    private async void OnMakeHttpRequest(object? sender, EventArgs e)
    {
        var client = httpClientFactory.CreateClient("sample");

        try
        {
            // Nothing listens on port 9, so this fails — which is the interesting path: the handler
            // reports the resource as failed and marks the span as an error, then rethrows so the
            // caller's own error handling still runs.
            await client.GetAsync("http://localhost:9/orders");
            Write("The request succeeded, which is unexpected.");
        }
        catch (Exception exception)
        {
            Write($"The request failed with {exception.GetType().Name}, and was reported as a "
                  + "failed RUM resource and an errored span.");
        }
    }

    private void OnPauseRecording(object? sender, EventArgs e)
    {
        sessionReplay.StopRecording();
        Write("Paused Session Replay. The session continues and RUM events keep flowing.");
    }

    private void OnResumeRecording(object? sender, EventArgs e)
    {
        sessionReplay.StartRecording();
        Write("Resumed Session Replay.");
    }

    private void OnSignIn(object? sender, EventArgs e)
    {
        // Applies to every feature at once: RUM sessions, logs and spans all carry it.
        Datadog.SetUser("user-123", "Ada Lovelace", "ada@example.com", new Dictionary<string, object?>
        {
            ["plan"] = "enterprise",
        });

        Datadog.SetAccount("acme-inc", "ACME Inc.");

        Write("Signed in. Every subsequent event carries the user and account.");
    }

    private void OnSignOut(object? sender, EventArgs e)
    {
        Datadog.ClearUser();
        Datadog.ClearAccount();

        // A new session, so the signed-out activity is not attributed to whoever was signed in.
        rum.StopSession();

        Write("Signed out and ended the RUM session.");
    }

    private async void OnPushDetails(object? sender, EventArgs e)
    {
        // No tracking code: pushing the page reports a view, popping it stops it.
        await Navigation.PushAsync(new DetailsPage());
    }

    private void Write(string message)
    {
        output.Insert(0, message + Environment.NewLine);
        OutputLabel.Text = output.ToString();
    }
}
