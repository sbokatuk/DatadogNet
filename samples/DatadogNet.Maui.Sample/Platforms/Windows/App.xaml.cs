namespace DatadogNet.Maui.Sample.WinUI;

/// <summary>
/// The Windows entry point.
/// </summary>
/// <remarks>
/// The interesting thing about this head is what it does not contain: the shared
/// <c>MauiProgram</c> calls <c>UseDatadog</c> unconditionally, and on Windows that resolves to
/// the documented silent no-op — so this file existing at all is the proof that a multi-headed
/// app needs no platform conditionals around Datadog.
/// </remarks>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
