using System.IO.Compression;

namespace DatadogNet.PackageTests;

/// <summary>What one package in this repository is supposed to be.</summary>
/// <param name="Id">The NuGet package id, which is also its directory under <c>src/</c>.</param>
/// <param name="Heads">Which target-framework families it ships — <c>all</c> or <c>mobile</c>.</param>
/// <param name="Dependencies">Other packages in this repository it must declare a dependency on.</param>
public sealed record PackageSpec(string Id, string Heads, IReadOnlyList<string> Dependencies);

/// <summary>
/// Locates the packed <c>.nupkg</c> files and describes what each one is supposed to contain.
/// </summary>
/// <remarks>
/// The package set is read from <c>build/packages.tsv</c> rather than repeated here, so a package
/// added to the build but not to the tests fails as a missing file instead of passing unnoticed.
/// </remarks>
public static class Packages
{
    /// <summary>Every package this repository builds, in dependency order.</summary>
    public static readonly IReadOnlyList<PackageSpec> All = ReadManifest();

    /// <summary>
    /// The target frameworks a package with <c>all</c> heads must carry an assembly for.
    /// </summary>
    /// <remarks>
    /// The neutral <c>net8.0</c>, <c>net9.0</c> and <c>net10.0</c> entries are the ones worth
    /// asserting hardest. They are what lets a MAUI app's Windows head, and a unit test, restore the
    /// package at all, and nothing about the build would fail if the platform-neutral pass silently
    /// stopped producing them — the platform assets would still be there and the package would still
    /// look complete.
    /// </remarks>
    public static readonly string[] AllHeadTargetFrameworks =
    [
        "net8.0",
        "net8.0-android34.0",
        "net8.0-ios18.0",
        "net8.0-maccatalyst18.0",
        "net9.0",
        "net9.0-android35.0",
        "net9.0-ios18.0",
        "net9.0-maccatalyst18.0",
        "net10.0",
        "net10.0-android36.0",
        "net10.0-ios26.0",
        "net10.0-maccatalyst26.0",
    ];

    /// <summary>The target frameworks a package with <c>mobile</c> heads must carry.</summary>
    public static readonly string[] MobileHeadTargetFrameworks =
    [
        "net8.0-android34.0",
        "net8.0-ios18.0",
        "net8.0-maccatalyst18.0",
        "net9.0-android35.0",
        "net9.0-ios18.0",
        "net9.0-maccatalyst18.0",
        "net10.0-android36.0",
        "net10.0-ios26.0",
        "net10.0-maccatalyst26.0",
    ];

    /// <summary>The DatadogNet.Android packages the android assets must depend on.</summary>
    /// <remarks>
    /// Asserted by name because getting this graph wrong is invisible until an app fails at
    /// runtime: a missing SessionReplayMaterial reference does not stop anything compiling, it
    /// makes Session Replay record a MAUI app as a screen of blank boxes.
    /// <para>
    /// OpenTracing left this list in 3.x, where dd-sdk-android removed the dependency outright.
    /// </para>
    /// </remarks>
    public static readonly string[] AndroidBindingDependencies =
    [
        "DatadogNet.Core.Android",
        "DatadogNet.Logs.Android",
        "DatadogNet.RUM.Android",
        "DatadogNet.SessionReplay.Android",
        "DatadogNet.SessionReplayMaterial.Android",
        "DatadogNet.Trace.Android",
    ];

    /// <summary>The DatadogNet.iOS packages the iOS assets must depend on.</summary>
    /// <remarks>
    /// Five, where the 2.x façade needed one. dd-sdk-ios 3.0 dissolved <c>DatadogObjc</c> — it
    /// survives only as an empty compatibility meta-package — so each module is referenced directly.
    /// Asserted by name because depending on the meta-package instead would restore and build
    /// perfectly well, and would quietly reintroduce an indirection that exists for someone else.
    /// </remarks>
    public static readonly string[] IosBindingDependencies =
    [
        "DatadogNet.Core.iOS",
        "DatadogNet.Logs.iOS",
        "DatadogNet.RUM.iOS",
        "DatadogNet.SessionReplay.iOS",
        "DatadogNet.Trace.iOS",
    ];

    /// <summary>The DatadogNet.Mac packages the maccatalyst assets must depend on.</summary>
    /// <remarks>
    /// The same five modules as iOS: the maccatalyst head compiles the Platforms/iOS
    /// implementation, so it calls into exactly the same types - supplied by the DatadogNet.Mac
    /// packages, whose binding definitions are verbatim copies of the iOS ones over
    /// Catalyst-built natives. SessionReplay is in the list although Datadog does not support
    /// replay on Catalyst, because the shared implementation references its types and must link.
    /// </remarks>
    public static readonly string[] MacBindingDependencies =
    [
        "DatadogNet.Core.Mac",
        "DatadogNet.Logs.Mac",
        "DatadogNet.RUM.Mac",
        "DatadogNet.SessionReplay.Mac",
        "DatadogNet.Trace.Mac",
    ];

    /// <summary>xunit member data: one row per package.</summary>
    public static TheoryData<string> Ids
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var package in All)
            {
                data.Add(package.Id);
            }

            return data;
        }
    }

    public static PackageSpec Spec(string id) => All.Single(package => package.Id == id);

    /// <summary>The target frameworks a package is expected to carry.</summary>
    public static string[] ExpectedTargetFrameworks(string id) =>
        Spec(id).Heads == "mobile" ? MobileHeadTargetFrameworks : AllHeadTargetFrameworks;

    public static ZipArchive OpenPackage(string id)
    {
        var path = Path.Combine(ArtifactsDirectory, $"{id}.{Version}.nupkg");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"'{path}' does not exist. Run ./build/BuildNugets.sh first.", path);
        }

        return ZipFile.OpenRead(path);
    }

    public static string ReadNuspec(ZipArchive package, string id)
    {
        var entry = package.GetEntry($"{id}.nuspec")
            ?? throw new InvalidOperationException($"{id} has no {id}.nuspec.");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// The version the packages were built with, read from <c>Directory.Build.props</c>.
    /// </summary>
    /// <remarks>
    /// Overridable so a CI job that packed a prerelease can point the tests at it without the
    /// version being written down in two places.
    /// </remarks>
    public static string Version =>
        Environment.GetEnvironmentVariable("DATADOG_PACKAGE_VERSION") is { Length: > 0 } configured
            ? configured
            : ReadVersionFromProps();

    /// <summary>The directory packages are read from.</summary>
    public static string ArtifactsDirectory =>
        Environment.GetEnvironmentVariable("DATADOG_ARTIFACTS_DIR") is { Length: > 0 } configured
            ? configured
            : Path.Combine(RepositoryRoot, "artifacts");

    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DatadogNet.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException(
                    "Could not find the repository root by walking up from " + AppContext.BaseDirectory);
        }
    }

    private static IReadOnlyList<PackageSpec> ReadManifest()
    {
        var path = Path.Combine(RepositoryRoot, "build", "packages.tsv");
        var specs = new List<PackageSpec>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length < 3)
            {
                throw new InvalidOperationException($"Malformed row in packages.tsv: '{line}'");
            }

            var dependencies = columns[2] == "-"
                ? []
                : columns[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            specs.Add(new PackageSpec(columns[0], columns[1], dependencies));
        }

        if (specs.Count == 0)
        {
            throw new InvalidOperationException("packages.tsv listed no packages.");
        }

        return specs;
    }

    private static string ReadVersionFromProps()
    {
        var props = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Build.props"));

        var native = Between(props, "<DatadogNativeVersion>", "</DatadogNativeVersion>");
        var revision = Between(props, "<DatadogBindingRevision>", "</DatadogBindingRevision>");

        return $"{native}.{revision}";

        static string Between(string text, string open, string close)
        {
            var start = text.IndexOf(open, StringComparison.Ordinal);
            if (start < 0)
            {
                throw new InvalidOperationException($"Directory.Build.props has no {open}");
            }

            start += open.Length;
            var end = text.IndexOf(close, start, StringComparison.Ordinal);

            return text[start..end].Trim();
        }
    }
}
