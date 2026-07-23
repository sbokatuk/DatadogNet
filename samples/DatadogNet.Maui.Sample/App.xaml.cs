namespace DatadogNet.Maui.Sample;

/// <summary>The application object.</summary>
public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services)
    {
        this.services = services;
        InitializeComponent();
    }

    /// <summary>
    /// Builds the window and its navigation stack.
    /// </summary>
    /// <remarks>
    /// A <see cref="NavigationPage"/> rather than a Shell, to make one thing visible: page tracking
    /// works the same either way. It subscribes to <c>Application.PageAppearing</c>, which fires for
    /// every page however it is shown — unlike Shell's <c>Navigated</c> or
    /// <c>NavigationPage.Pushed</c>, each of which sees only its own navigation.
    /// </remarks>
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(services.GetRequiredService<MainPage>()));
}
