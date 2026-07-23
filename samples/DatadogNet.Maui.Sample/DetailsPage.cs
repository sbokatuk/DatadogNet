namespace DatadogNet.Maui.Sample;

/// <summary>
/// A second page, to show that navigation alone produces RUM views.
/// </summary>
/// <remarks>
/// Not a line of Datadog code in it. Pushing it opens a view named <c>Details</c>, popping it closes
/// one — which is exactly what neither native SDK can do on its own, because a MAUI page is not a
/// <c>UIViewController</c> and not an <c>Activity</c>.
/// <para>
/// Built in C# rather than XAML to make the point that the attached property is optional: this page
/// sets its name through <see cref="DatadogTracking.SetViewName"/> instead.
/// </para>
/// </remarks>
public sealed class DetailsPage : ContentPage
{
    public DetailsPage()
    {
        Title = "Details";

        DatadogTracking.SetViewName(this, "Details");

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "This page reports itself as a RUM view named 'Details' with no code of "
                           + "its own. Go back and the view is stopped.",
                    FontSize = 15,
                },
                new Button { Text = "Back", Command = new Command(async () => await Navigation.PopAsync()) },
            },
        };
    }
}
