# Real-device iOS builds and the missing `_OBJC_CLASS_$_` symbols

Status: **root-caused and fixed downstream** in DatadogNet.iOS **3.14.0.5** (generated linker
aliases + up-front class realization, shipped inside the binding packages); reported upstream to
`DataDog/dd-sdk-ios`. Discovered 2026-07-28 while checking the MAUI sample against real hardware;
the investigation that followed corrected several conclusions of the first written analysis, so
this file replaces it.

## Symptom

Building `DatadogNet.Maui.Sample` — or any consuming app — for `ios-arm64` (a physical iPhone)
failed at the native link:

```
error : Undefined symbols for architecture arm64:
  "_OBJC_CLASS_$_DDConfiguration", referenced from: in registrar.o
  "_OBJC_CLASS_$_DDLogEvent", referenced from: in registrar.o
  ... (10 more DDRUM* types in the sample's case)
```

The identical build for `iossimulator-arm64` linked and ran fine — which is all either
repository's CI exercised at the time.

## Root cause (verified on the pristine upstream archives)

dd-sdk-ios's release pipeline builds the prebuilt `Datadog.xcframework.zip` **device** slices
with a **12.0 deployment target**, while the **simulator** slices get **14.0** (the
arm64-simulator floor):

```
device:    minos 12.0
simulator: minos 14.0
```

Below iOS 13, the Swift compiler withholds *static Objective-C registration* for `@objc` classes
whose metadata needs runtime fix-ups: no `_OBJC_CLASS_$_<Name>` symbol, no `__objc_classlist`
entry. The class is meant to be realized lazily from the Swift side. **41 classes** across
DatadogCore (5), DatadogLogs (1), DatadogRUM (31), DatadogTrace (3) and DatadogSessionReplay (1)
are exported by every simulator slice and absent from every device slice; the .NET static
registrar references bound classes by exactly those names.

Facts that corrected the original analysis:

- **The class objects are not gone.** Each is present in the device slice — and *exported* —
  under its Swift metadata symbol (`_$s…CN`); the simulator slice exports the same object under
  both names at the same address. The original "nothing left to alias" conclusion was a grep
  artifact: the Swift names don't contain the `DD*` strings.
- **This is not a 3.14.0 regression, and pinning back does not help.** 3.13.0, the
  `-with-arm64e` twin archives, and even 2.30.2 (the previously bound line, in its `DatadogObjc`
  framework) all carry the same asymmetry. Every DatadogNet.iOS package published before
  3.14.0.5 — and the 2.x-era bindings before it — fails device links the same way.
- **Nobody upstream had noticed** (no issue, no fix in flight, 3.14.0 is the latest release):
  SwiftPM/CocoaPods consumers compile dd-sdk-ios from source with their app's own deployment
  target, so only consumers of the *prebuilt binaries* — Carthage, and bindings like these —
  can hit it, and only on device builds.
- **Lazy is not merely lazy.** On hardware, `objc_getClass("DDConfiguration")` returns nil and a
  cold `[DDConfiguration class]` on the raw metadata is a **segfault**; after one call to the
  class's exported Swift metadata accessor (`_$s…CMa`) the very same address is a fully working,
  name-resolvable class. This is why the fix has two halves.

## The fix (in DatadogNet.iOS ≥ 3.14.0.5)

Per affected class, the binding packages inject one app-link flag —
`-Wl,-alias,<swift metadata symbol>,_OBJC_CLASS_$_<Name>` — so the registrar's references
resolve against the exported Swift metadata; and each binding assembly embeds the same table and
realizes every listed class through its metadata accessor in a module initializer, before
anything can message it. The table is regenerated mechanically from the binaries on every native
bump, a Mach-O symbol audit in the package tests fails on drift, and both repositories' CI now
links for a real device (the exact failure above is a red build today, on both SDK bands).
Details: `DatadogNet.iOS/docs/release-notes/3.14.0.5.md` and
`DatadogNet.iOS/build/device-class-aliases/README.md`.

Verified end-to-end on a physical iPhone 15 Pro Max: install, launch, SDK initialization, and
runtime name-resolution of the previously missing classes.

For this repository: device builds need DatadogNet.iOS packages at **3.14.0.5 or later**, which is
where `DatadogNetiOSVersion` now sits — so a consuming app gets the repair by restoring, with no
project changes. Nothing in the managed layer changes.

## Upstream

Reported to `DataDog/dd-sdk-ios` with the evidence (deployment-target asymmetry, the 41-class
inventory, and the release-validation gap — their `validate-xcframeworks.sh` checks file
existence only). The requested fix — building the archived device slices with a ≥ 13.0
deployment target — makes the whole downstream mechanism disappear: the alias generator then
produces empty tables and the packages stop carrying flags. `upstream-drift.yml` will surface
the release that includes it.
