using Microsoft.Extensions.Configuration;
using Xunit;

namespace DatadogNet.UnitTests;

/// <summary>
/// <see cref="DatadogConfigurationBinder"/>: the appsettings path into
/// <see cref="DatadogConfiguration"/>.
/// </summary>
public class ConfigurationBinderTests
{
    [Fact]
    public void Binds_a_full_section()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "production",
            ["Datadog:Service"] = "my-app",
            ["Datadog:Site"] = "eu1",
            ["Datadog:TrackingConsent"] = "Granted",
            ["Datadog:CrashReportsEnabled"] = "false",
            ["Datadog:FirstPartyHosts:api.example.com:0"] = "Datadog",
            ["Datadog:FirstPartyHosts:api.example.com:1"] = "TraceContext",
            ["Datadog:AdditionalConfiguration:_dd.telemetry.configuration_sample_rate"] = "100",
            ["Datadog:Rum:ApplicationId"] = "rum-app",
            ["Datadog:Rum:SessionSampleRate"] = "20.5",
            ["Datadog:Rum:LongTaskThreshold"] = "0:00:00.25",
            ["Datadog:Logs:CustomEndpoint"] = "https://proxy.example.com/logs",
            ["Datadog:Trace:SampleRate"] = "50",
            ["Datadog:Trace:GlobalTags:team"] = "mobile",
            ["Datadog:Trace:HeaderTypes:0"] = "TraceContext",
            ["Datadog:SessionReplay:TextAndInputPrivacy"] = "MaskSensitiveInputs",
        });

        var bound = DatadogConfigurationBinder.Bind(configuration.GetSection("Datadog"));

        Assert.Equal("token", bound.ClientToken);
        Assert.Equal("production", bound.Env);
        Assert.Equal("my-app", bound.Service);
        Assert.Equal(DatadogSite.Eu1, bound.Site);
        Assert.Equal(TrackingConsent.Granted, bound.TrackingConsent);
        Assert.False(bound.CrashReportsEnabled);
        Assert.NotNull(bound.FirstPartyHosts);
        TracingHeaderType[] expectedHostHeaders = [TracingHeaderType.Datadog, TracingHeaderType.TraceContext];
        Assert.Equal(expectedHostHeaders, bound.FirstPartyHosts!["api.example.com"]);
        Assert.Equal("100", bound.AdditionalConfiguration!["_dd.telemetry.configuration_sample_rate"]);
        Assert.NotNull(bound.Rum);
        Assert.Equal("rum-app", bound.Rum!.ApplicationId);
        Assert.Equal(20.5f, bound.Rum.SessionSampleRate);
        Assert.Equal(TimeSpan.FromMilliseconds(250), bound.Rum.LongTaskThreshold);
        Assert.Equal(new Uri("https://proxy.example.com/logs"), bound.Logs!.CustomEndpoint);
        Assert.Equal(50f, bound.Trace!.SampleRate);
        Assert.Equal("mobile", bound.Trace.GlobalTags!["team"]);
        TracingHeaderType[] expectedHeaderTypes = [TracingHeaderType.TraceContext];
        Assert.Equal(expectedHeaderTypes, bound.Trace.HeaderTypes);
        Assert.Equal(TextAndInputPrivacy.MaskSensitiveInputs, bound.SessionReplay!.TextAndInputPrivacy);
    }

    [Fact]
    public void A_feature_section_that_is_absent_stays_disabled()
    {
        var bound = DatadogConfigurationBinder.Bind(Minimal());

        Assert.Null(bound.Rum);
        Assert.Null(bound.Logs);
        Assert.Null(bound.Trace);
        Assert.Null(bound.SessionReplay);
        Assert.Null(bound.FirstPartyHosts);
    }

    [Fact]
    public void Defaults_come_from_the_option_types_not_from_the_binder()
    {
        // The drift guard: bind sections that exist but set nothing, and every value must equal
        // the option type's own default. If someone changes a default in RumOptions, this fails
        // rather than the binder silently shipping the old one.
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "test",
            ["Datadog:Rum:ApplicationId"] = "rum-app",
            ["Datadog:Trace:Service"] = "svc",
            ["Datadog:SessionReplay:SampleRate"] = "100",
        });

        var bound = DatadogConfigurationBinder.Bind(configuration.GetSection("Datadog"));
        var defaults = new DatadogConfiguration { ClientToken = "token", Env = "test" };
        var rumDefaults = new RumOptions { ApplicationId = "rum-app" };
        var traceDefaults = new TraceOptions();
        var replayDefaults = new SessionReplayOptions();

        Assert.Equal(defaults.Site, bound.Site);
        Assert.Equal(defaults.TrackingConsent, bound.TrackingConsent);
        Assert.Equal(defaults.BatchSize, bound.BatchSize);
        Assert.Equal(defaults.UploadFrequency, bound.UploadFrequency);
        Assert.Equal(defaults.BatchProcessingLevel, bound.BatchProcessingLevel);
        Assert.Equal(defaults.CrashReportsEnabled, bound.CrashReportsEnabled);
        Assert.Equal(rumDefaults.SessionSampleRate, bound.Rum!.SessionSampleRate);
        Assert.Equal(rumDefaults.TelemetrySampleRate, bound.Rum.TelemetrySampleRate);
        Assert.Equal(rumDefaults.TrackFrustrations, bound.Rum.TrackFrustrations);
        Assert.Equal(rumDefaults.VitalsUpdateFrequency, bound.Rum.VitalsUpdateFrequency);
        Assert.Equal(rumDefaults.LongTaskThreshold, bound.Rum.LongTaskThreshold);
        Assert.Equal(traceDefaults.SampleRate, bound.Trace!.SampleRate);
        Assert.Equal(traceDefaults.BundleWithRumEnabled, bound.Trace.BundleWithRumEnabled);
        Assert.Equal(traceDefaults.HeaderTypes, bound.Trace.HeaderTypes);
        Assert.Equal(replayDefaults.TextAndInputPrivacy, bound.SessionReplay!.TextAndInputPrivacy);
        Assert.Equal(replayDefaults.ImagePrivacy, bound.SessionReplay.ImagePrivacy);
        Assert.Equal(replayDefaults.TouchPrivacy, bound.SessionReplay.TouchPrivacy);
        Assert.Equal(replayDefaults.StartRecordingImmediately, bound.SessionReplay.StartRecordingImmediately);
    }

    [Fact]
    public void A_missing_client_token_names_its_path()
    {
        var configuration = Build(new Dictionary<string, string?> { ["Datadog:Env"] = "test" });

        var error = Assert.Throws<ArgumentException>(
            () => DatadogConfigurationBinder.Bind(configuration.GetSection("Datadog")));

        Assert.Contains("Datadog:ClientToken", error.Message);
    }

    [Fact]
    public void A_misspelt_enum_names_its_path_and_the_choices()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "test",
            ["Datadog:Site"] = "Europe",
        });

        var error = Assert.Throws<ArgumentException>(
            () => DatadogConfigurationBinder.Bind(configuration.GetSection("Datadog")));

        Assert.Contains("Datadog:Site", error.Message);
        Assert.Contains("Eu1", error.Message);
    }

    [Fact]
    public void An_empty_long_task_threshold_disables_rather_than_defaults()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "test",
            ["Datadog:Rum:ApplicationId"] = "rum-app",
            ["Datadog:Rum:LongTaskThreshold"] = "",
        });

        var bound = DatadogConfigurationBinder.Bind(configuration.GetSection("Datadog"));

        Assert.Null(bound.Rum!.LongTaskThreshold);
    }

    [Fact]
    public void The_service_collection_overload_binds_validates_and_registers()
    {
        // The bound configuration flows through the same Initialize as every other overload, so a
        // section that binds but fails validation - RUM present, sample rate out of range - throws
        // at startup rather than silently misreporting.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "test",
            ["Datadog:Rum:ApplicationId"] = "rum-app",
            ["Datadog:Rum:SessionSampleRate"] = "250",
        });

        Assert.Throws<ArgumentException>(
            () => services.AddDatadog(configuration.GetSection("Datadog")));
    }

    private static IConfiguration Minimal() =>
        Build(new Dictionary<string, string?>
        {
            ["Datadog:ClientToken"] = "token",
            ["Datadog:Env"] = "test",
        }).GetSection("Datadog");

    private static IConfigurationRoot Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
