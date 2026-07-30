---
applyTo: "src/**/*.cs"
---

# Façade sources

- One shared API, one implementation per platform. Declare the type and its members in the shared
  source at the project root, mark the platform-varying parts `partial`, and supply the bodies under
  `Platforms/Android`, `Platforms/iOS` and `Platforms/Unsupported`. `src/Datadog.Facade.props`
  selects exactly one directory per target framework; a head that matches none fails to compile,
  which is the intended outcome.
- `Platforms/iOS` is also compiled for **maccatalyst**, against the DatadogNet.Mac bindings. Any edit
  there must hold for both. Do not add a `Platforms/MacCatalyst` directory — `DatadogNet.Maui` is the
  sole exception, because MAUI's single-project targets strip `Platforms/iOS` from non-iOS heads.
- `Platforms/Unsupported` is what the neutral `net8.0`/`net9.0`/`net10.0` assemblies link. Every
  member there is a **documented** no-op, so a Windows or plain-.NET head restores and runs. Adding a
  member to the shared API means adding it here too.
- Document every public member with XML docs. `CS1591` is not suppressed and `TreatWarningsAsErrors`
  makes a missing doc comment a build failure.
- No reflection, no dynamic code generation: `EnableTrimAnalyzer` and `EnableAotAnalyzer` are on for
  every head and their warnings are errors. The configuration binder is hand-rolled for this reason.
- Nullable reference types and implicit usings are on; `LangVersion` is `latest`.
- Add a `PackageReference` for every binding module the code calls into, versioned through
  `$(DatadogNetiOSVersion)`, `$(DatadogNetAndroidVersion)` or `$(DatadogNetMacVersion)` — never a
  literal version, and never a prerelease.
- Logic with no native dependency belongs in the shared sources, where `tests/DatadogNet.UnitTests`
  can exercise it through `InternalsVisibleTo` in milliseconds.
