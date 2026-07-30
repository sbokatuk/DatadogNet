# DatadogNet — repository instructions

## What this is

- The cross-platform umbrella of the DatadogNet family: **one Datadog API for .NET MAUI** — RUM,
  Logs, Trace, Session Replay and crash reporting on Android, iOS and Mac Catalyst from shared code.
- It binds **nothing** native. It is a façade over three sibling binding repositories, consumed as
  NuGet packages: [DatadogNet.iOS](https://github.com/sbokatuk/DatadogNet.iOS),
  [DatadogNet.Android](https://github.com/sbokatuk/DatadogNet.Android),
  [DatadogNet.Mac](https://github.com/sbokatuk/DatadogNet.Mac).
- Six packages, listed once in `build/packages.tsv`: `DatadogNet`, `DatadogNet.CrashReporting`,
  `DatadogNet.WebView`, `DatadogNet.Maui`, `DatadogNet.Extensions.DependencyInjection`,
  `DatadogNet.Extensions.Diagnostics`.
- Version **3.14.0.6** = `DatadogNativeVersion` + `DatadogBindingRevision` in
  `Directory.Build.props`. It names the **dd-sdk-ios** line (Android is dd-sdk-android 3.12.1); the
  fourth component is this repository's own revision.
- Platform pins, same file: `DatadogNetiOSVersion` 3.14.0.5, `DatadogNetAndroidVersion` 3.12.1.3
  (`DatadogAndroidNativeVersion` 3.12.1), `DatadogNetMacVersion` 3.14.0.4. NuGet packs each as a
  **minimum** (`>= x.y.z.r`), not an exact range: floating above a pin is deliberately supported for
  emergency binding patches, so never call these exact pins. The iOS pin is a floor with teeth —
  3.14.0.5 is the first revision that links for a real device
  (`docs/device-arm64-missing-objc-class-symbols.md`).
- Target frameworks come from `DatadogSdkBand` (default `net9`): the net9 band builds `net8.0;net9.0`
  plus the android/ios/maccatalyst heads of both, the net10 band builds `net10.0`,
  `net10.0-android36.0`, `net10.0-ios26.0`, `net10.0-maccatalyst26.0`. `DatadogNet.Maui` also ships
  windows heads and no neutral asset.
- OS floors: iOS 12.2 (Swift ABI), Android API 23 (androidx.savedstate 1.4.0, not the .aars' 21),
  Mac Catalyst 15.0.

## Build and verify

Requires **macOS** (every package has an iOS head), an Xcode from the **26.0** line (see
`.github/actions/select-xcode`), the .NET 9 and .NET 10 SDKs with the `android`, `ios`,
`maccatalyst` and `maui-*` workloads, and Android platforms 34/35/36. `global.json` pins SDK 9.0.100
(`rollForward: latestFeature`), so the net10 work runs from a scratch directory with its own
`global.json`.

- `dotnet test tests/DatadogNet.UnitTests` — seconds, and the first thing to run for any change to
  shared sources. The suite is `net9.0`, but its `ProjectReference`s multi-target, so the mobile
  workloads must be installed or restore fails with NETSDK1147. No workflow runs it: run it locally.
- `./build/BuildNugets.sh [version]` — packs every row of `build/packages.tsv` twice (net9 band,
  net10 band), then `build/merge-packages.py` grafts the net10 assets in. Output: `artifacts/`.
- `dotnet test tests/DatadogNet.PackageTests` — only meaningful after packing; set
  `DATADOG_PACKAGE_VERSION` when the packages were packed at a version other than `VersionPrefix`.
- `./build/CheckReadmeVersions.sh` — run before pushing any version change; it is the first step of
  the `pack` job and fails the build when README.md disagrees with `Directory.Build.props`.
- `./.github/scripts/run-simulator-tests.sh <version> [tfm]` and
  `./.github/scripts/run-emulator-tests.sh <version> [tfm]` — device checks against packed packages.
- Sample: `dotnet build samples/DatadogNet.Maui.Sample/DatadogNet.Maui.Sample.csproj -f
  net10.0-ios26.0 -p:DatadogPackageVersion=<version>`, from a directory whose `global.json` pins
  .NET 10. The sample targets net10 only and is deliberately outside `DatadogNet.sln`.

## Layout

- `src/` — the six packages, plus `Datadog.Facade.props`, which every package `.csproj` imports.
  Shared sources declare the API; `Platforms/Android`, `Platforms/iOS` and `Platforms/Unsupported`
  supply the bodies, exactly one per target framework.
- `build/` — `packages.tsv` (the package roster), `upstream.tsv` (what the drift check watches) and
  the scripts: `BuildNugets.sh`, `CheckReadmeVersions.sh`, `check-upstream.sh`, `merge-packages.py`.
- `tests/` — `DatadogNet.UnitTests` (net9.0 xunit over the neutral no-op head, reached through
  `InternalsVisibleTo`), `DatadogNet.PackageTests` (shape of the packed `.nupkg`s),
  `DatadogNet.DeviceTests` (one app, android + ios heads, run on emulator and simulator),
  `DatadogNet.Maui.WindowsHeadCheck` (XAML-free MAUI class library proving the packed windows stub
  restores and compiles from macOS).
- `samples/DatadogNet.Maui.Sample/` — consumes packed nupkgs from the local `artifacts` feed
  declared in `NuGet.config`; not in the solution.
- `docs/` — design and upgrade notes (see References) plus `release-notes/<version>.md`.
- `.github/` — `workflows/`, `actions/select-xcode/`, `scripts/`.

## Conventions

- Versions are four-part. A façade-only change bumps `DatadogBindingRevision`; a platform re-pin
  that moves a native line resets it to 1 and updates `DatadogNativeVersion` /
  `DatadogAndroidNativeVersion` alongside.
- Every version needs `docs/release-notes/<4-part-version>.md`: it ships as `PackageReleaseNotes`
  and becomes the GitHub release body.
- New public API goes in the shared sources with `partial` bodies in every `Platforms/` directory,
  `Platforms/Unsupported`'s documented no-op included.
- Document every public member: `CS1591` is deliberately not suppressed and `TreatWarningsAsErrors`
  makes it fatal. Keep shared sources reflection-free — the trim and AOT analysers are on everywhere.
- British spelling in README and docs ("licence", "initialise", "behaviour").
- Adding a package = one row in `build/packages.tsv` plus a project under `src/`. Nothing else keeps
  a copy of the list.

## CI and release flow

- `pr.yml` → `build.yml` with `verify: true`, packing `<version>-beta.<pr>.<run>` and publishing it
  to nuget.org (fork PRs build but skip publish).
- `build.yml` (reusable, `workflow_call`): `pack` on macos-15 (README check, select-xcode, pack,
  package tests, upload artifact), `e2e-ios` simulator matrix, `e2e-android` emulator matrix on
  ubuntu, `sample`, and `link-release` (android and ios Release link checks).
- Release: merging a PR that **adds** `docs/release-notes/<version>.md` makes `auto-release.yml` tag
  that merge commit and dispatch `release.yml`, whose `guard` job proves the tag is an ancestor of
  the default branch before `build.yml` runs with `verify: false` and the packages are published
  alongside a GitHub release. A hand-pushed `v*` tag takes the same path.
- Publishing is nuget.org **trusted publishing**: OIDC through `NuGet/login@v1`, the only secret is
  `NUGET_USER`, and the jobs need `id-token: write` and the `nuget.org` environment.
- `upstream-drift.yml` runs `build/check-upstream.sh` daily over `build/upstream.tsv`, whose `nuget`
  rows watch the three sibling package sets. A pin **behind** nuget.org is a finding; **ahead** is
  the normal mid-release-train state.

## Testing

- Shared-source change → unit tests. Packaging, target-framework or `packages.tsv` change → pack,
  then package tests. Platform implementation or binding re-pin → device tests on both platforms.
  MAUI change → the sample; windows-head change → `DatadogNet.Maui.WindowsHeadCheck`.
- Device tests, the sample and the windows-head check consume packed nupkgs from `artifacts/`. Never
  convert them to `ProjectReference`: that bypasses the published dependency graph under test.
- Re-pack before re-running a device check at the same version; the runner scripts clear the NuGet
  cache and the app's `obj/`/`bin/` because a same-version repack is otherwise invisible.

## Hard rules

- Never commit native artifacts or packed `.nupkg`s. This repository binds nothing — natives arrive
  inside the pinned binding packages — and `artifacts/` is git-ignored and created by the build.
- A version bump must update the README — install snippets, dd-sdk badges, architecture diagram,
  device-check examples and the Version map row. `./build/CheckReadmeVersions.sh` enforces it.
- Re-pin `DatadogNetiOSVersion` / `DatadogNetAndroidVersion` / `DatadogNetMacVersion` only to
  versions published on nuget.org, and keep iOS and Mac on the **same dd-sdk-ios release** (first
  three components) — the shared `Platforms/iOS` implementation compiles against both.
- Pin **stable** versions only. `pr.yml` publishes `-beta.<pr>.<run>` builds of every package, so a
  prerelease pin is one typo away; because the pin is a floor, it would also make prereleases
  eligible for consumers. A `-beta.*` pin must never reach a release-note merge or a tag.
- Never break Mac Catalyst when editing `Platforms/iOS` — the maccatalyst heads compile those same
  sources. Do not add a `Platforms/MacCatalyst` directory; the sole exception is `DatadogNet.Maui`,
  where MAUI's single-project targets strip `Platforms/iOS` from non-iOS heads and its `.csproj`
  documents the re-include.
- Release only through the workflows. Never publish by hand, never bypass the `guard` job's ancestry
  check: `verify: false` on the release path is legitimate only because the pull request verified
  that exact commit.
- Do not suppress `CS1591`, and do not pin the AndroidX graph here to silence `NU1608` — pinning
  would force that generation on every consumer, a mistake documented in `src/Datadog.Facade.props`.
- Keep `build/packages.tsv` the only package roster; workflows, scripts and tests all read it.

## References

- Native SDKs: [DataDog/dd-sdk-ios](https://github.com/DataDog/dd-sdk-ios),
  [DataDog/dd-sdk-android](https://github.com/DataDog/dd-sdk-android),
  [Datadog RUM docs](https://docs.datadoghq.com/real_user_monitoring/).
- Siblings: [DatadogNet.iOS](https://github.com/sbokatuk/DatadogNet.iOS),
  [DatadogNet.Android](https://github.com/sbokatuk/DatadogNet.Android),
  [DatadogNet.Mac](https://github.com/sbokatuk/DatadogNet.Mac).
- In-repo: `README.md` and `docs/` — `native-surface.md`, `upgrade-to-3x.md`, `gap-analysis-3x.md`,
  `upstream-changes.md`, `swift-interop-plan.md`, `device-arm64-missing-objc-class-symbols.md`.
- The long comments in `Directory.Build.props`, `src/Datadog.Facade.props` and each workflow record
  why things are as they are; read them before changing what they explain.

Trust these instructions. Search the codebase only when something here is incomplete or turns out to
be wrong.
