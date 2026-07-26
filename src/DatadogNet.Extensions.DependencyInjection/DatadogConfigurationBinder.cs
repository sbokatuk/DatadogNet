using Microsoft.Extensions.Configuration;

namespace DatadogNet;

/// <summary>
/// Builds a <see cref="DatadogConfiguration"/> from an <c>IConfiguration</c> section.
/// </summary>
/// <remarks>
/// <see cref="DatadogConfiguration"/> is <c>required</c>-and-<c>init</c> shaped, which the general
/// reflection binder cannot construct — and a hand-rolled binder can also be precise about errors:
/// a missing <c>ClientToken</c> or a misspelt enum names the full configuration path instead of
/// binding half an object. Keys are the property names, features are enabled by their section
/// being present, and every default comes from the option types themselves, so this class states
/// no default twice:
/// <code>
/// {
///   "Datadog": {
///     "ClientToken": "…",
///     "Env": "production",
///     "Site": "Eu1",
///     "TrackingConsent": "Pending",
///     "Rum": { "ApplicationId": "…", "SessionSampleRate": 20 },
///     "Logs": {},
///     "Trace": { "HeaderTypes": [ "Datadog", "TraceContext" ] },
///     "FirstPartyHosts": { "api.example.com": [ "Datadog", "TraceContext" ] }
///   }
/// }
/// </code>
/// The delegate hooks (<c>ConfigureNative</c>) cannot come from configuration; set them in code by
/// building on the bound object with a <c>with</c>-style copy of your own if you need both.
/// </remarks>
public static class DatadogConfigurationBinder
{
    /// <summary>
    /// Binds <paramref name="configuration"/> — typically <c>Configuration.GetSection("Datadog")</c>
    /// — into a validated-shape <see cref="DatadogConfiguration"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A required key is missing, or a value does not parse; the message carries the full
    /// configuration path.
    /// </exception>
    public static DatadogConfiguration Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaults = new DatadogConfiguration { ClientToken = string.Empty, Env = string.Empty };

        return new DatadogConfiguration
        {
            ClientToken = Required(configuration, "ClientToken"),
            Env = Required(configuration, "Env"),
            Service = configuration["Service"],
            Site = ReadEnum(configuration, "Site", defaults.Site),
            TrackingConsent = ReadEnum(configuration, "TrackingConsent", defaults.TrackingConsent),
            Verbosity = ReadEnum(configuration, "Verbosity", defaults.Verbosity),
            BatchSize = ReadEnum(configuration, "BatchSize", defaults.BatchSize),
            UploadFrequency = ReadEnum(configuration, "UploadFrequency", defaults.UploadFrequency),
            BatchProcessingLevel = ReadEnum(configuration, "BatchProcessingLevel", defaults.BatchProcessingLevel),
            Variant = configuration["Variant"] ?? defaults.Variant,
            CrashReportsEnabled = ReadBool(configuration, "CrashReportsEnabled", defaults.CrashReportsEnabled),
            FirstPartyHosts = ReadFirstPartyHosts(configuration.GetSection("FirstPartyHosts")),
            AdditionalConfiguration = ReadAdditionalConfiguration(configuration.GetSection("AdditionalConfiguration")),
            Rum = ReadRum(configuration.GetSection("Rum")),
            Logs = ReadLogs(configuration.GetSection("Logs")),
            Trace = ReadTrace(configuration.GetSection("Trace")),
            SessionReplay = ReadSessionReplay(configuration.GetSection("SessionReplay")),
        };
    }

    private static RumOptions? ReadRum(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var defaults = new RumOptions { ApplicationId = string.Empty };

        return new RumOptions
        {
            ApplicationId = Required(section, "ApplicationId"),
            SessionSampleRate = ReadFloat(section, "SessionSampleRate", defaults.SessionSampleRate),
            TelemetrySampleRate = ReadFloat(section, "TelemetrySampleRate", defaults.TelemetrySampleRate),
            TrackFrustrations = ReadBool(section, "TrackFrustrations", defaults.TrackFrustrations),
            TrackBackgroundEvents = ReadBool(section, "TrackBackgroundEvents", defaults.TrackBackgroundEvents),
            TrackAnonymousUser = ReadBool(section, "TrackAnonymousUser", defaults.TrackAnonymousUser),
            VitalsUpdateFrequency = ReadEnum(section, "VitalsUpdateFrequency", defaults.VitalsUpdateFrequency),
            LongTaskThreshold = ReadLongTaskThreshold(section, defaults.LongTaskThreshold),
            TrackAutomaticInstrumentation = ReadBool(
                section, "TrackAutomaticInstrumentation", defaults.TrackAutomaticInstrumentation),
            CustomEndpoint = ReadUri(section, "CustomEndpoint"),
        };
    }

    private static LogsOptions? ReadLogs(IConfigurationSection section) =>
        section.Exists()
            ? new LogsOptions { CustomEndpoint = ReadUri(section, "CustomEndpoint") }
            : null;

    private static TraceOptions? ReadTrace(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var defaults = new TraceOptions();

        return new TraceOptions
        {
            SampleRate = ReadFloat(section, "SampleRate", defaults.SampleRate),
            Service = section["Service"],
            NetworkInfoEnabled = ReadBool(section, "NetworkInfoEnabled", defaults.NetworkInfoEnabled),
            BundleWithRumEnabled = ReadBool(section, "BundleWithRumEnabled", defaults.BundleWithRumEnabled),
            GlobalTags = ReadStringDictionary(section.GetSection("GlobalTags")),
            HeaderTypes = ReadHeaderTypes(section.GetSection("HeaderTypes")) ?? defaults.HeaderTypes,
            CustomEndpoint = ReadUri(section, "CustomEndpoint"),
        };
    }

    private static SessionReplayOptions? ReadSessionReplay(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var defaults = new SessionReplayOptions();

        return new SessionReplayOptions
        {
            SampleRate = ReadFloat(section, "SampleRate", defaults.SampleRate),
            TextAndInputPrivacy = ReadEnum(section, "TextAndInputPrivacy", defaults.TextAndInputPrivacy),
            ImagePrivacy = ReadEnum(section, "ImagePrivacy", defaults.ImagePrivacy),
            TouchPrivacy = ReadEnum(section, "TouchPrivacy", defaults.TouchPrivacy),
            StartRecordingImmediately = ReadBool(
                section, "StartRecordingImmediately", defaults.StartRecordingImmediately),
            CustomEndpoint = ReadUri(section, "CustomEndpoint"),
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TracingHeaderType>>? ReadFirstPartyHosts(
        IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var hosts = new Dictionary<string, IReadOnlyList<TracingHeaderType>>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in section.GetChildren())
        {
            hosts[host.Key] = ReadHeaderTypes(host)
                ?? throw new ArgumentException(
                    $"'{host.Path}' must list at least one tracing header type, e.g. [\"Datadog\", \"TraceContext\"].");
        }

        return hosts;
    }

    private static IReadOnlyList<TracingHeaderType>? ReadHeaderTypes(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var types = section.GetChildren()
            .Select(child => ParseEnum<TracingHeaderType>(child.Value, child.Path))
            .ToList();

        return types.Count > 0 ? types : null;
    }

    private static IReadOnlyDictionary<string, string>? ReadStringDictionary(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var child in section.GetChildren())
        {
            values[child.Key] = child.Value ?? string.Empty;
        }

        return values;
    }

    private static IReadOnlyDictionary<string, object?>? ReadAdditionalConfiguration(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var child in section.GetChildren())
        {
            values[child.Key] = child.Value;
        }

        return values;
    }

    /// <summary>
    /// Absent means the option type's default; explicitly empty means disabled — the two things
    /// "no value" can mean for a nullable threshold, kept distinguishable.
    /// </summary>
    private static TimeSpan? ReadLongTaskThreshold(IConfigurationSection section, TimeSpan? fallback)
    {
        var raw = section["LongTaskThreshold"];

        if (raw is null)
        {
            return fallback;
        }

        if (raw.Length == 0)
        {
            return null;
        }

        return TimeSpan.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"'{section.Path}:LongTaskThreshold' is \"{raw}\", which is not a TimeSpan. " +
                "Use \"0:00:00.1\" for 100 ms, or an empty value to disable long-task tracking.");
    }

    private static string Required(IConfiguration section, string key) =>
        section[key] is { Length: > 0 } value
            ? value
            : throw new ArgumentException(
                $"'{Path(section, key)}' is required and missing. " +
                "Bind the section that actually holds the Datadog settings, e.g. GetSection(\"Datadog\").");

    private static bool ReadBool(IConfiguration section, string key, bool fallback) =>
        section[key] is { Length: > 0 } raw
            ? bool.TryParse(raw, out var parsed)
                ? parsed
                : throw new ArgumentException($"'{Path(section, key)}' is \"{raw}\", which is not true or false.")
            : fallback;

    private static float ReadFloat(IConfiguration section, string key, float fallback) =>
        section[key] is { Length: > 0 } raw
            ? float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw new ArgumentException($"'{Path(section, key)}' is \"{raw}\", which is not a number.")
            : fallback;

    private static Uri? ReadUri(IConfiguration section, string key) =>
        section[key] is { Length: > 0 } raw
            ? Uri.TryCreate(raw, UriKind.Absolute, out var parsed)
                ? parsed
                : throw new ArgumentException($"'{Path(section, key)}' is \"{raw}\", which is not an absolute URI.")
            : null;

    private static TEnum ReadEnum<TEnum>(IConfiguration section, string key, TEnum fallback)
        where TEnum : struct, Enum =>
        section[key] is { Length: > 0 } raw
            ? ParseEnum<TEnum>(raw, Path(section, key))
            : fallback;

    private static TEnum ParseEnum<TEnum>(string? raw, string path)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException(
                $"'{path}' is \"{raw}\", which is not one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");

    private static string Path(IConfiguration section, string key) =>
        section is IConfigurationSection s ? $"{s.Path}:{key}" : key;
}
