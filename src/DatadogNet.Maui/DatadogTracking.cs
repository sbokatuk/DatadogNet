using System.Runtime.CompilerServices;

namespace DatadogNet.Maui;

/// <summary>
/// Turns MAUI page navigation into RUM views.
/// </summary>
/// <remarks>
/// Attached automatically by <see cref="MauiAppBuilderExtensions.UseDatadog"/> unless
/// <see cref="DatadogMauiOptions.TrackPageViews"/> is turned off. The public members here are for
/// naming pages and for the unusual app that wants to drive attachment itself.
/// </remarks>
public static class DatadogTracking
{
    /// <summary>
    /// Names the RUM view for a page, overriding the type name.
    /// </summary>
    /// <remarks>
    /// <code>
    /// &lt;ContentPage xmlns:dd="clr-namespace:DatadogNet.Maui;assembly=DatadogNet.Maui"
    ///              dd:DatadogTracking.ViewName="Checkout"&gt;
    /// </code>
    /// Worth setting where the type name is not what you would search for — a
    /// <c>ProductDetailPage</c> that everyone calls "Product", or two pages of the same type
    /// reached by different routes.
    /// </remarks>
    public static readonly BindableProperty ViewNameProperty = BindableProperty.CreateAttached(
        "ViewName",
        typeof(string),
        typeof(DatadogTracking),
        defaultValue: null);

    /// <summary>
    /// Excludes a page from view tracking.
    /// </summary>
    /// <remarks>
    /// For a page that is not a screen in the user's terms — a transparent overlay, a loading shim,
    /// a host page whose only job is to contain another. Each of those would otherwise open and
    /// close a RUM view, splitting the real screen's time in two.
    /// </remarks>
    public static readonly BindableProperty IsExcludedProperty = BindableProperty.CreateAttached(
        "IsExcluded",
        typeof(bool),
        typeof(DatadogTracking),
        defaultValue: false);

    /// <summary>
    /// Every page currently being reported as a view, and the scope that will stop it.
    /// </summary>
    /// <remarks>
    /// A <see cref="ConditionalWeakTable{TKey, TValue}"/> so that a page which disappears without
    /// its <c>Disappearing</c> ever firing — a modal dismissed by the platform, a page dropped when
    /// its navigation stack is replaced — does not keep itself alive through this map. The view is
    /// then left open until something else stops it, which is the same outcome as the native SDKs'
    /// own trackers and better than leaking the page.
    /// </remarks>
    private static readonly ConditionalWeakTable<Page, IRumViewScope> OpenViews = new();

    private static readonly object AttachGate = new();

    private static DatadogMauiOptions options = new();

    private static Application? attached;

    /// <summary>Reads <see cref="ViewNameProperty"/>.</summary>
    public static string? GetViewName(BindableObject page) =>
        (string?)page.GetValue(ViewNameProperty);

    /// <summary>Sets <see cref="ViewNameProperty"/>.</summary>
    public static void SetViewName(BindableObject page, string? value) =>
        page.SetValue(ViewNameProperty, value);

    /// <summary>Reads <see cref="IsExcludedProperty"/>.</summary>
    public static bool GetIsExcluded(BindableObject page) =>
        (bool)page.GetValue(IsExcludedProperty);

    /// <summary>Sets <see cref="IsExcludedProperty"/>.</summary>
    public static void SetIsExcluded(BindableObject page, bool value) =>
        page.SetValue(IsExcludedProperty, value);

    /// <summary>
    /// Starts reporting a RUM view for each page of <paramref name="application"/>.
    /// </summary>
    /// <remarks>
    /// Idempotent, and idempotent per application instance: attaching twice — which the platform
    /// hooks can genuinely do, since an Android activity is recreated on rotation — subscribes once.
    /// <para>
    /// <c>PageAppearing</c> and <c>PageDisappearing</c> on <see cref="Application"/> rather than
    /// Shell's <c>Navigated</c> or <c>NavigationPage.Pushed</c>: those two see only their own
    /// navigation, and an app that uses Shell for its tabs and modals for its flows would report
    /// half its screens. The application-level pair fires for every page however it is shown.
    /// </para>
    /// </remarks>
    /// <param name="application">The running application.</param>
    public static void Attach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        lock (AttachGate)
        {
            if (ReferenceEquals(attached, application))
            {
                return;
            }

            if (attached is not null)
            {
                attached.PageAppearing -= OnPageAppearing;
                attached.PageDisappearing -= OnPageDisappearing;
            }

            application.PageAppearing += OnPageAppearing;
            application.PageDisappearing += OnPageDisappearing;
            attached = application;
        }
    }

    /// <summary>Stops reporting views for pages, and closes any that are open.</summary>
    public static void Detach()
    {
        lock (AttachGate)
        {
            if (attached is null)
            {
                return;
            }

            attached.PageAppearing -= OnPageAppearing;
            attached.PageDisappearing -= OnPageDisappearing;
            attached = null;
        }
    }

    internal static void Configure(DatadogMauiOptions value) => options = value;

    /// <summary>
    /// Attaches to <see cref="Application.Current"/> if there is one.
    /// </summary>
    /// <remarks>
    /// Called from the platform lifecycle hooks, which fire once the application object exists —
    /// unlike <c>MauiAppBuilder.Build()</c>, where <see cref="Application.Current"/> is still null.
    /// Returns quietly rather than throwing when there is nothing to attach to, because the hooks
    /// fire on paths that do not always have a MAUI application behind them.
    /// </remarks>
    internal static void AttachToCurrentApplication()
    {
        if (Application.Current is { } application)
        {
            Attach(application);
        }
    }

    private static void OnPageAppearing(object? sender, Page page)
    {
        if (page is null || GetIsExcluded(page))
        {
            return;
        }

        // Already open: PageAppearing fires again for the page underneath when a modal above it is
        // dismissed, and starting a second view for the same key would close the first with a
        // duration of zero.
        if (OpenViews.TryGetValue(page, out _))
        {
            return;
        }

        var name = ResolveName(page);

        // The key is per page *instance*, not per name: two instances of the same page type can be
        // on the stack at once - a product page pushed from a product page - and a shared key would
        // make stopping one close the other.
        var key = $"{name}#{RuntimeHelpers.GetHashCode(page):x8}";

        OpenViews.Add(page, Datadog.Rum.StartView(key, name));
    }

    private static void OnPageDisappearing(object? sender, Page page)
    {
        if (page is null || !OpenViews.TryGetValue(page, out var scope))
        {
            return;
        }

        OpenViews.Remove(page);
        scope.Dispose();
    }

    private static string ResolveName(Page page) =>
        GetViewName(page)
            ?? options.ViewName?.Invoke(page)
            ?? page.GetType().Name;
}
