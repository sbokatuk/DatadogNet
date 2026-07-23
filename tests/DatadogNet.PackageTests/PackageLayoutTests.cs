using System.Xml.Linq;

namespace DatadogNet.PackageTests;

/// <summary>
/// Asserts the shape of the produced NuGet packages.
/// </summary>
/// <remarks>
/// These run against the packed <c>.nupkg</c> rather than the build output, so they catch packaging
/// regressions the compiler cannot see — a target framework the merge step dropped, a dependency
/// group that came out empty, a licence file that stopped being included.
/// </remarks>
public class PackageLayoutTests
{
    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_carries_an_assembly_for_every_expected_target_framework(string id)
    {
        using var package = Packages.OpenPackage(id);

        foreach (var tfm in Packages.ExpectedTargetFrameworks(id))
        {
            Assert.True(
                package.GetEntry($"lib/{tfm}/{id}.dll") is not null,
                $"{id} is missing 'lib/{tfm}/{id}.dll'. The net10 assets come from the second pack " +
                "pass and are grafted in by merge-packages.py, so a missing net10 target framework " +
                "usually means that step did not run.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_carries_no_target_framework_it_should_not(string id)
    {
        using var package = Packages.OpenPackage(id);

        var expected = Packages.ExpectedTargetFrameworks(id).ToHashSet();

        var actual = package.Entries
            .Select(entry => entry.FullName.Split('/'))
            .Where(parts => parts.Length > 2 && parts[0] == "lib")
            .Select(parts => parts[1])
            .ToHashSet();

        // Equality, not containment, in both directions: a target framework silently disappearing
        // from the merge step is as bad as one appearing that nothing was built or tested for.
        Assert.Equal(expected.OrderBy(tfm => tfm), actual.OrderBy(tfm => tfm));
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_carries_documentation_for_every_assembly(string id)
    {
        using var package = Packages.OpenPackage(id);

        foreach (var tfm in Packages.ExpectedTargetFrameworks(id))
        {
            var entry = package.GetEntry($"lib/{tfm}/{id}.xml");

            Assert.True(entry is not null, $"{id} is missing 'lib/{tfm}/{id}.xml'.");

            // The API is the product here, and its documentation is where every platform difference
            // is recorded. A few hundred bytes would mean the file was emitted but empty.
            Assert.True(entry!.Length > 1_000, $"'{entry.FullName}' is only {entry.Length} bytes.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_declares_a_dependency_group_for_every_target_framework(string id)
    {
        using var package = Packages.OpenPackage(id);

        var groups = DependencyGroups(Packages.ReadNuspec(package, id));

        foreach (var tfm in Packages.ExpectedTargetFrameworks(id))
        {
            Assert.True(
                groups.ContainsKey(tfm),
                $"{id}'s nuspec declares no dependency group for '{tfm}'. NuGet reads a missing " +
                "group as 'this target framework needs nothing', so the platform bindings would " +
                "not be restored and the app would fail to link.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_depends_on_its_siblings_at_this_exact_version(string id)
    {
        using var package = Packages.OpenPackage(id);

        var groups = DependencyGroups(Packages.ReadNuspec(package, id));
        var expected = Packages.Spec(id).Dependencies;

        foreach (var tfm in Packages.ExpectedTargetFrameworks(id))
        {
            foreach (var dependency in expected)
            {
                var matches = groups[tfm].Where(d => d.Id == dependency).ToList();

                Assert.True(matches.Count == 1, $"{id} ({tfm}) does not depend on {dependency}.");

                // The version being packed, as a floor. NuGet resolves a bare version as a minimum
                // and picks the lowest that satisfies every constraint, so with all four published
                // together this is what a consumer gets. It is asserted anyway because the number
                // comes from the pack pass rather than from the .csproj: a merge step that carried
                // a dependency group across from the wrong pass would show up here first.
                Assert.Equal(Packages.Version, matches[0].Version);
            }
        }
    }

    [Fact]
    public void Android_assets_depend_on_every_Datadog_Android_binding_they_call()
    {
        using var package = Packages.OpenPackage("DatadogNet");

        var groups = DependencyGroups(Packages.ReadNuspec(package, "DatadogNet"));

        foreach (var tfm in Packages.ExpectedTargetFrameworks("DatadogNet").Where(t => t.Contains("android")))
        {
            var declared = groups[tfm].Select(d => d.Id).ToHashSet();

            foreach (var binding in Packages.AndroidBindingDependencies)
            {
                Assert.True(
                    declared.Contains(binding),
                    $"DatadogNet ({tfm}) does not depend on {binding}.");
            }
        }
    }

    [Fact]
    public void iOS_assets_depend_on_every_Datadog_iOS_binding_they_call()
    {
        using var package = Packages.OpenPackage("DatadogNet");

        var groups = DependencyGroups(Packages.ReadNuspec(package, "DatadogNet"));

        foreach (var tfm in Packages.ExpectedTargetFrameworks("DatadogNet").Where(t => t.Contains("ios")))
        {
            var declared = groups[tfm].Select(d => d.Id).ToHashSet();

            foreach (var binding in Packages.IosBindingDependencies)
            {
                Assert.True(declared.Contains(binding), $"DatadogNet ({tfm}) does not depend on {binding}.");
            }

            // The 2.x façade took DatadogObjc, which re-exported the whole SDK. In 3.x that package
            // is an empty compatibility shim for apps migrating a PackageReference, and depending on
            // it here would work while meaning something quite different.
            Assert.DoesNotContain("DatadogNet.Objc.iOS", declared);
        }
    }

    [Fact]
    public void Mac_Catalyst_assets_depend_on_every_Datadog_Mac_binding_they_call()
    {
        using var package = Packages.OpenPackage("DatadogNet");

        var groups = DependencyGroups(Packages.ReadNuspec(package, "DatadogNet"));

        foreach (var tfm in Packages.ExpectedTargetFrameworks("DatadogNet").Where(t => t.Contains("maccatalyst")))
        {
            var declared = groups[tfm].Select(d => d.Id).ToHashSet();

            foreach (var binding in Packages.MacBindingDependencies)
            {
                Assert.True(declared.Contains(binding), $"DatadogNet ({tfm}) does not depend on {binding}.");
            }

            // The maccatalyst head must take the .Mac bindings, never the .iOS ones: the iOS
            // packages carry no maccatalyst assets, so a stray reference would fail restore in
            // every consuming app - or worse, resolve to nothing and fail at link.
            Assert.DoesNotContain(declared, id => id.EndsWith(".iOS", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Neutral_assets_depend_on_no_platform_binding()
    {
        using var package = Packages.OpenPackage("DatadogNet");

        var groups = DependencyGroups(Packages.ReadNuspec(package, "DatadogNet"));

        foreach (var tfm in new[] { "net8.0", "net9.0", "net10.0" })
        {
            // The whole point of the neutral asset is that a Windows head (or a unit test) can
            // restore it. A stray platform binding dependency would make that fail with NU1202,
            // and nothing in the build would notice - the platform heads would still work.
            Assert.Empty(groups[tfm]);
        }
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_declares_the_combined_licence_and_ships_both_texts(string id)
    {
        using var package = Packages.OpenPackage(id);

        var nuspec = XDocument.Parse(Packages.ReadNuspec(package, id));
        var ns = nuspec.Root!.GetDefaultNamespace();

        Assert.Equal(
            "MIT AND Apache-2.0",
            nuspec.Descendants(ns + "license").Single().Value);

        // NuGet resolves a licence per package rather than per graph, so a package whose
        // dependencies ship Apache-2.0 native binaries has to carry the text itself.
        Assert.NotNull(package.GetEntry("licenses/MIT.txt"));
        Assert.NotNull(package.GetEntry("licenses/Apache-2.0.txt"));
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_carries_a_readme_and_an_icon(string id)
    {
        using var package = Packages.OpenPackage(id);

        Assert.NotNull(package.GetEntry("README.md"));
        Assert.NotNull(package.GetEntry("icon.png"));
    }

    [Theory]
    [MemberData(nameof(Packages.Ids), MemberType = typeof(Packages))]
    public void Package_has_a_symbol_package(string id)
    {
        var path = Path.Combine(Packages.ArtifactsDirectory, $"{id}.{Packages.Version}.snupkg");

        Assert.True(File.Exists(path), $"'{path}' does not exist.");
    }

    [Fact]
    public void The_MAUI_package_ships_no_platform_neutral_asset()
    {
        using var package = Packages.OpenPackage("DatadogNet.Maui");

        // Asserted rather than merely documented, because it is the one asymmetry in the package
        // set and the reason for it is not obvious: Microsoft.Maui.Controls is a workload
        // metapackage with no lib/ assets, so there is nothing to compile a neutral no-op against.
        // If a future MAUI does ship one, this test failing is the prompt to reconsider.
        Assert.Null(package.GetEntry("lib/net9.0/DatadogNet.Maui.dll"));
        Assert.Null(package.GetEntry("lib/net10.0/DatadogNet.Maui.dll"));
    }

    /// <summary>Maps each target framework to the dependencies its nuspec group declares.</summary>
    private static Dictionary<string, IReadOnlyList<(string Id, string Version)>> DependencyGroups(string nuspec)
    {
        var document = XDocument.Parse(nuspec);
        var ns = document.Root!.GetDefaultNamespace();

        return document
            .Descendants(ns + "group")
            .ToDictionary(
                group => group.Attribute("targetFramework")!.Value,
                group => (IReadOnlyList<(string, string)>)
                    [.. group.Elements(ns + "dependency")
                        .Select(dependency => (
                            dependency.Attribute("id")!.Value,
                            dependency.Attribute("version")!.Value))]);
    }
}
