# DatadogNet

[![NuGet](https://img.shields.io/nuget/v/DatadogNet?label=nuget)](https://www.nuget.org/packages/DatadogNet)
[![release](https://github.com/sbokatuk/DatadogNet/actions/workflows/release.yml/badge.svg)](https://github.com/sbokatuk/DatadogNet/actions/workflows/release.yml)
[![Targets: net9.0 | net10.0](https://img.shields.io/badge/targets-net9.0%20%7C%20net10.0-512BD4)](#packages)
[![dd-sdk-ios 2.30.2](https://img.shields.io/badge/dd--sdk--ios-2.30.2-632CA6)](https://github.com/DataDog/dd-sdk-ios/releases/tag/2.30.2)
[![dd-sdk-android 2.26.3](https://img.shields.io/badge/dd--sdk--android-2.26.3-632CA6)](https://github.com/DataDog/dd-sdk-android/releases/tag/2.26.3)
[![Licence: MIT AND Apache-2.0](https://img.shields.io/badge/licence-MIT%20AND%20Apache--2.0-green)](#licence)

**One Datadog API for .NET MAUI.** Real User Monitoring, Logs, Trace, Session Replay and crash
reporting on Android and iOS, written once in shared code.

Built on [DatadogNet.iOS](https://github.com/sbokatuk/DatadogNet.iOS) and
[DatadogNet.Android](https://github.com/sbokatuk/DatadogNet.Android), which bind the native SDKs.
Those two are faithful projections of Kotlin and Objective-C, and consequently share almost no
shape: `Configuration.Builder(...).UseSite(...).Build()` against `new DDConfiguration { Site = … }`,
`GlobalRumMonitor.Get()` against `DDRUMMonitor.Shared`, `io.opentracing` against `OTTracer`. This
package is the layer that makes a MAUI app not care.

```bash
dotnet add package DatadogNet.Maui
```

```csharp
builder
    .UseMauiApp<App>()
    .UseDatadog(new DatadogConfiguration
    {
        ClientToken     = "<CLIENT_TOKEN>",
        Env             = "production",
        Service         = "my-app",
        Site            = DatadogSite.Us1,
        TrackingConsent = TrackingConsent.Granted,
        Rum             = new RumOptions { ApplicationId = "<RUM_APPLICATION_ID>" },
        Logs            = new LogsOptions(),
    });
```

That is the whole setup. Pages become RUM views, `ILogger` output goes to Datadog, and unhandled
exceptions are reported — on both platforms.

---

## Contents

- [Packages](#packages)
- [Installing](#installing)
- [Usage](#usage)
- [What the MAUI package does for you](#what-the-maui-package-does-for-you)
- [Reaching the native SDK](#reaching-the-native-sdk)
- [Platform differences](#platform-differences)
- [How this repository works](#how-this-repository-works)
- [Building locally](#building-locally)
- [Upgrading](#upgrading)
- [Releasing](#releasing)
- [Troubleshooting](#troubleshooting)
- [Licence](#licence)

---

## Packages

Four packages. The version is `<dd-sdk-ios version>.<binding revision>` — `2.30.2.1` is dd-sdk-ios
**2.30.2**, binding revision **1**, and the Android side of the same release is dd-sdk-android
**2.26.3**.

> **Why one version names one SDK.** Datadog releases the iOS and Android SDKs on separate cadences
> and their version numbers have never matched. A façade over both has to pick one line to name
> itself after or invent a third numbering that maps to nothing. It picks iOS, because that is the
> line whose version is furthest ahead, and states the Android version everywhere it states its own.

| Package | What it is for | Depends on |
| --- | --- | --- |
| **`DatadogNet.Maui`** | **The one you install in a MAUI app.** `UseDatadog`, automatic RUM views from page navigation, an `ILogger` provider, unhandled-exception reporting, `HttpClient` instrumentation. | `DatadogNet` |
| `DatadogNet` | The API itself: configuration, RUM, Logs, Trace, Session Replay, consent, user and account info, `DatadogHttpMessageHandler`. Usable from a plain .NET Android or .NET iOS app with no MAUI. | — |
| `DatadogNet.CrashReporting` | Crash reporting. Separate because it installs a signal handler. | `DatadogNet` |
| `DatadogNet.WebView` | Bridges RUM and Logs out of a web view, for hybrid apps. | `DatadogNet` |

Most apps need one or two lines:

```xml
<PackageReference Include="DatadogNet.Maui" Version="2.30.2.1" />
<PackageReference Include="DatadogNet.CrashReporting" Version="2.30.2.1" />
```

**Target frameworks.** `DatadogNet`, `DatadogNet.CrashReporting` and `DatadogNet.WebView` ship
nine: `net8.0`, `net9.0` and `net10.0`, each with its `-android` and `-ios` head —
`net8.0-android34.0`, `net8.0-ios18.0`, `net9.0-android35.0`, `net9.0-ios18.0`,
`net10.0-android36.0`, `net10.0-ios26.0`. `DatadogNet.Maui` ships the six platform ones only.

The plain `net9.0`/`net10.0` assets are not padding. A MAUI app routinely also has a Windows or Mac
Catalyst head, and NuGet resolves each head independently; without a platform-neutral asset the
reference has to be guarded by target platform in every project that has one. With it, the reference
is unconditional and the Windows head links an implementation whose every member is a documented
no-op — so shared code that reports a RUM view compiles and runs there, and in a unit test, without
a single `#if`.

`DatadogNet.Maui` is the exception and cannot be otherwise: `Microsoft.Maui.Controls` is a workload
metapackage with no `lib/` assets, so there is no neutral MAUI to compile a no-op against.
A multi-headed app guards that one reference:

```xml
<ItemGroup Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == 'android' or '$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == 'ios'">
  <PackageReference Include="DatadogNet.Maui" Version="2.30.2.1" />
</ItemGroup>
```

> **net8 has one limitation, and it is worth reading before you rely on it.** The net8 assets work
> — the device checks run against them on both a simulator and an emulator — but a .NET **MAUI** app
> on net8 does not build, and no target-framework choice here can change that. Pointing the sample at
> `net8.0-android34.0` fails during Java callable wrapper generation:
>
> ```
> error XA4204: Unable to resolve interface type
> 'AndroidX.Navigation.NavController/IOnDestinationChangedListener'
> ```
>
> MAUI 8 resolves as **8.0.100** — the original November 2023 release, since no serviced MAUI 8
> exists in any installed workload band — and its AndroidX generation does not line up with the one
> the Datadog Android bindings require. It is the same class of failure `DatadogNet.Android`'s README
> records for MAUI 9, one version earlier and with a different symptom. MAUI 8 also reached
> [end of support on 14 May 2025](https://dotnet.microsoft.com/en-us/platform/support/policy/maui).
>
> So net8 is for a **plain .NET Android or .NET iOS app** — one with no MAUI — which is also what
> `DatadogNet.Android`'s and `DatadogNet.iOS`'s own net8 support is for. `DatadogNet.Maui` ships net8
> for uniformity, so that a net8 MAUI app fails with the Android SDK error above rather than a
> restore error blaming the wrong package, but it cannot be used from one. For MAUI, target net9 or
> net10.

**A net8 Android app needs one extra line.** Two packages in the AndroidX graph disagree about
`SavedState` — `Navigation.Common 2.9.6` wants `>= 1.4.0`, `SavedState.Ktx 1.3.2` pins
`[1.3.2, 1.3.3)`. The .NET 9 and .NET 10 SDKs resolve the higher version and warn (`NU1608`); the
.NET 8 SDK refuses with `NU1107`. Add the version those other bands already land on:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
  <PackageReference Include="Xamarin.AndroidX.SavedState" Version="1.4.0" />
</ItemGroup>
```

**A net8 Android app must also be built with the .NET 8 SDK**, not the .NET 9 one. The .NET 9 band
has the API 34 *reference* packs and compiles it, then fails at packaging with `NETSDK1112` because
it has no API 34 *runtime* packs — those belong to the .NET 8 workload. iOS has no equivalent
constraint: the .NET 9 band builds `net8.0-ios18.0` outright.

**Minimum OS**: Android API **23**, iOS **12.2**.

> The dd-sdk-android `.aar` manifests declare API 21, and `DatadogNet.Android`'s README records
> that as the floor. It is not reachable: those `.aar`s depend on an AndroidX generation in which
> `androidx.savedstate` declares `minSdkVersion 23`, and the manifest merger takes the maximum
> across the graph — so an app declaring 21 fails to build. 23 is the real floor.

---

## Installing

```xml
<ItemGroup>
  <PackageReference Include="DatadogNet.Maui" Version="2.30.2.1" />
</ItemGroup>
```

```xml
<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">23</SupportedOSPlatformVersion>
<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">12.2</SupportedOSPlatformVersion>
```

---

## Usage

[`samples/DatadogNet.Maui.Sample`](samples/DatadogNet.Maui.Sample) is a working two-headed MAUI app
that does everything below.

### Initialise

Once, as early as the app can — `MauiProgram.CreateMauiApp`, before anything that might throw.
Crash reporting only covers what happens after the SDK is up.

```csharp
builder.UseDatadog(new DatadogConfiguration
{
    ClientToken     = "<CLIENT_TOKEN>",
    Env             = "production",
    Service         = "my-app",
    Site            = DatadogSite.Us1,       // Us1, Us3, Us5, Eu1, Ap1, Ap2, Us1Fed
    TrackingConsent = TrackingConsent.Granted,

    Rum           = new RumOptions { ApplicationId = "<RUM_APPLICATION_ID>" },
    Logs          = new LogsOptions(),
    Trace         = new TraceOptions(),
    SessionReplay = new SessionReplayOptions(),

    FirstPartyHosts = new Dictionary<string, IReadOnlyList<TracingHeaderType>>
    {
        ["api.example.com"] = [TracingHeaderType.Datadog, TracingHeaderType.TraceContext],
    },
});

CrashReporting.Enable();   // DatadogNet.CrashReporting, after initialising
```

A feature is enabled by assigning its options object and left off by leaving it `null`. The ordering
both native SDKs require — core first, RUM before Session Replay — is this call's problem rather than
yours; getting it wrong natively fails silently, with the feature simply never producing data.

> **`TrackingConsent` defaults to `Pending`, not `Granted`.** Pending collects and holds without
> uploading, so an app that has not yet asked its user loses nothing by starting there and calling
> `Datadog.SetTrackingConsent` when it has an answer. Defaulting to granted uploads data the user has
> not agreed to, and no later call takes that back.

### RUM

Pages become views automatically. Report the rest yourself:

```csharp
using (Datadog.Rum.StartView("checkout", "Checkout"))
{
    Datadog.Rum.AddAction(RumActionType.Tap, "pay", new Dictionary<string, object?>
    {
        ["cart.total"] = 42.50m,
    });

    Datadog.Rum.AddTiming("cart-loaded");

    try { /* ... */ }
    catch (Exception exception) { Datadog.Rum.AddError(exception); }
}
```

The view scope is worth adopting everywhere. The native API on both platforms is a
`startView`/`stopView` pair matched by key, and a view left open by an early return or an exception
is not an error — it goes on collecting every later action and error in the session, attributed to a
screen the user has left.

Also on `Datadog.Rum`: `StartAction`/`StopAction` for continuous gestures, `StartResource` and
friends for network calls, `AddFeatureFlagEvaluation`, `AddAttribute`/`RemoveAttribute` for
session-wide attributes, `StopSession`, and `GetCurrentSessionIdAsync` — which is what turns "the app
was slow" in a support ticket into a session you can watch.

### Logs

```csharp
Datadog.Logger.Info("user signed in");
Datadog.Logger.Error("payment failed", exception, new Dictionary<string, object?>
{
    ["order.id"] = orderId,
});
```

Levels are `Debug`, `Info`, `Notice`, `Warn`, `Error`, `Critical`. `Datadog.Logs.CreateLogger` makes
a logger with a name, a service or a different sampling rate; `Datadog.Logger` is the one obvious
logger for apps that want no ceremony.

An exception's type, message and stack go over as Datadog's reserved `error.kind`, `error.message`
and `error.stack`, so the entry renders as an error rather than as three unrelated attributes.

### Trace

```csharp
using var span = Datadog.Tracer.StartSpan("checkout");
span.SetTag("cart.items", 3);

try { /* ... */ }
catch (Exception exception) { span.SetError(exception); throw; }
```

Disposing finishes the span, which is the point of the shape: a span that is never finished is never
sent. `span.Activate()` makes it the parent of spans started while it is active — following the
native SDKs' own thread-local scope managers, so it does **not** flow across an `await`.

`Datadog.Tracer.Inject(span)` returns the headers that continue the trace into your backend, in
whichever of `Datadog`, `B3`, `B3Multi` and `TraceContext` you configured.

### HTTP

```csharp
builder.Services
    .AddHttpClient<OrderService>(client => client.BaseAddress = new Uri("https://api.example.com"))
    .AddDatadogTracking();
```

Every request becomes a RUM resource, wrapped in a span, with tracing headers attached — but only on
hosts listed in `FirstPartyHosts`. Everything else is still reported as a resource, because you want
to see how slow a third-party API is, and is never given a trace id.

This is the one part of the façade with no native counterpart to delegate to, and it has to be:
neither SDK's automatic network instrumentation can see an `HttpClient` call. Android's
`DatadogInterceptor` is an OkHttp interceptor and `HttpClient` does not route through OkHttp by
default; iOS's `DDURLSessionInstrumentation` hooks an `NSURLSession` delegate class and
`NSUrlSessionHandler` owns its delegate rather than exposing it.

Without dependency injection, `new HttpClient(new DatadogHttpMessageHandler(new HttpClientHandler()))`.

### Session Replay

```csharp
SessionReplay = new SessionReplayOptions
{
    SampleRate           = 100,
    TextAndInputPrivacy  = TextAndInputPrivacy.MaskAll,
    ImagePrivacy         = ImagePrivacy.MaskAll,
    TouchPrivacy         = TouchPrivacy.Hide,
},
```

Requires RUM. The three levels decide what is redacted **on the device**, before anything is
uploaded, and they default to the most private setting of each — so the default is safe rather than
convenient. Loosen them deliberately.

Set `StartRecordingImmediately = false` to enable the feature without recording, then
`Datadog.SessionReplay.StartRecording()` once the user has agreed.

On Android the Material extension is registered for you, because MAUI's Android handlers are built
on Material Components and without it a MAUI app records as a screen of blank boxes.

### Consent, user and account

```csharp
Datadog.SetTrackingConsent(TrackingConsent.Granted);
Datadog.SetUser("user-123", "Ada Lovelace", "ada@example.com");
Datadog.SetAccount("acme-inc", "ACME Inc.");
Datadog.ClearUser();      // on sign-out
Datadog.ClearAllData();   // for a "delete my data" request
```

---

## What the MAUI package does for you

Each of these is something you would otherwise write twice, once per platform.

**RUM views from page navigation.** Neither native SDK can do this: a MAUI page is not a
`UIViewController` and not an `Activity`. Android draws the whole app into one Activity, so
`ActivityViewTrackingStrategy` reports a single view for the entire session; iOS reports per view
controller, which for a Shell app is roughly per page but never matches the route names your code
uses. `DatadogNet.Maui` subscribes to `Application.PageAppearing`/`PageDisappearing` — which fire for
every page however it is shown, unlike Shell's `Navigated` or `NavigationPage.Pushed`, each of which
sees only its own navigation.

Name a page whatever you would search for:

```xml
<ContentPage xmlns:dd="clr-namespace:DatadogNet.Maui;assembly=DatadogNet.Maui"
             dd:DatadogTracking.ViewName="Checkout">
```

or exclude one that is not a screen in the user's terms with `dd:DatadogTracking.IsExcluded="True"`.

**An `ILogger` provider.** An app that already logs through `ILogger<T>` gets its logs into Datadog
with no call-site changes, each entry tagged with its category and correlated with the RUM view it
was written in.

**Unhandled exceptions as RUM errors.** Complements crash reporting rather than replacing it. A
managed exception reaching `AppDomain.UnhandledException` still has its .NET type and managed frames;
by the time the same failure arrives at a native crash handler it is a signal with those frames
flattened away. `TaskScheduler.UnobservedTaskException` is picked up too, which does not terminate
the process and so is otherwise invisible.

**Dependency injection.** `IRumMonitor`, `IDatadogLogs`, `IDatadogLogger`, `IDatadogTracer` and
`ISessionReplay` are registered as singletons, so a view-model can depend on the piece it uses and be
given a substitute in a test rather than reaching for the static.

---

## Reaching the native SDK

The façade covers every documented feature both SDKs share, which is not all of either. Where it
stops, it does not become a dead end: every options type has a `ConfigureNative` hook that receives
the native configuration object at the one moment its setting can still be applied.

```csharp
Rum = new RumOptions
{
    ApplicationId = "…",
    ConfigureNative = native =>
    {
#if ANDROID
        ((Com.Datadog.Android.Rum.RumConfiguration.Builder)native)
            .TrackNonFatalAnrs(true)
            .CollectAccessibility(true);
#elif IOS
        ((DatadogObjc.DDRUMConfiguration)native).TrackWatchdogTerminations = true;
#endif
    },
},
```

This matters more than it looks, because several native settings are **configuration time only** and
cannot be reached after `Datadog.Initialize` returns. RUM's five event mappers —
`SetViewEventMapper`, `SetActionEventMapper`, `SetResourceEventMapper`, `SetErrorEventMapper`,
`SetLongTaskEventMapper` — are the supported way to redact or drop an event on the device before it
is uploaded, and they can only be set here. They are not lifted into `RumOptions` because the event
models they hand you are large, entirely different between the two SDKs, and generated from separate
schemas; a cross-platform version would be a third schema to maintain and would still not let you
touch the fields only one platform has.

[`docs/native-surface.md`](docs/native-surface.md) lists, per feature, what is lifted into the façade
and what is deliberately left to `ConfigureNative` — including the settings that exist on only one
platform.

---

## Platform differences

The façade hides the API differences. It does not pretend the platforms are identical, and these are
the places where they are not.

| | Android (dd-sdk-android 2.26.3) | iOS (dd-sdk-ios 2.30.2) |
| --- | --- | --- |
| **Crash reporting** | JVM crashes are built in and on by default (`CrashReportsEnabled`), which covers an unhandled .NET exception. `DatadogNet.CrashReporting` adds native (NDK) crashes. | Nothing is built in. Without `DatadogNet.CrashReporting` an iOS app reports no crashes at all. |
| **`ImagePrivacy.MaskContentImages`** | Masks images larger than roughly 100×100 dp. | Masks images not bundled with the app. |
| **Automatic view tracking** | One view for the whole app, because MAUI uses a single Activity. Still worth having: it reports app start and ANRs. | Roughly one view per page, through the default UIKit predicate. |
| **RUM action types** | Also has `Click` and `Back`. | — |
| **`DatadogVerbosity.None`** | A threshold above every `android.util.Log` priority. | A real `DDSDKVerbosityLevel.None`. |
| **Only there** | `TrackNonFatalAnrs`, `CollectAccessibility`, `SetSlowFramesConfiguration`, the other view-tracking strategies. | `TrackWatchdogTerminations`, `AppHangThreshold`, the SwiftUI predicates. |
| **Sites** | Also has `STAGING`, which is Datadog's own internal environment. | — |

Reachable through `ConfigureNative` in every case.

Two things that look like they should differ and do not: tracing is OpenTracing-shaped on **both**
platforms in the 2.x line — `AndroidTracer` implements `io.opentracing.Tracer` and `DDTracer`
implements `OTTracer` — which is why `IDatadogSpan` can be one interface rather than two shapes glued
together. And the three Session Replay privacy levels are the same three, with the same names, in the
same order, apart from the middle image level above.

> **This is the 2.x line of both SDKs, and that is not merely "older".** dd-sdk 3.0 removed
> OpenTracing on both platforms, and dd-sdk-ios 3.0 deleted the `DatadogObjc` façade this repository's
> iOS head is written against and replaced PLCrashReporter with KSCrash. Moving to 3.x is a rewrite
> of both platform layers, not a version bump.
>
> The usual argument for staying on 2.x — that it keeps Android API 21 as its floor, where 3.0
> raised it to 23 — does not actually hold: the AndroidX generation 2.x's `.aar`s depend on forces
> 23 anyway. See [Packages](#packages). The other reasons still do.

---

## How this repository works

```
nuget.org: DatadogNet.iOS 2.30.2.2        nuget.org: DatadogNet.Android 2.26.3.1
        │  (dd-sdk-ios 2.30.2)                    │  (dd-sdk-android 2.26.3)
        └──────────────┬──────────────────────────┘
                       ▼
        src/DatadogNet/Platforms/{iOS,Android,Unsupported}/
                       │  one shared API, three implementations, one selected per target framework
                       ▼
        src/DatadogNet.{CrashReporting,WebView,Maui}/
                       │  build/BuildNugets.sh — pack twice (net9 band, net10 band), then merge
                       ▼
                artifacts/*.nupkg  ──►  nuget.org
```

Nothing native is bound here and nothing is downloaded: both platform package sets come from
nuget.org, pinned to an exact version rather than a range. They are pinned because this layer calls
into the hand-written convenience APIs in both repositories — `RumMonitorExtensions`,
`DDRUMMonitor.Ergonomics` and friends — which no compatibility promise covers, and a floating
reference would turn a change there into a build break in somebody's app rather than here.

Building this façade also turned up a set of gaps in those two repositories, including one that made
iOS tracing unusable from C# outright. They were fixed there rather than worked around here —
[`docs/upstream-changes.md`](docs/upstream-changes.md) is the list and the reasoning. It is why the
iOS pin is `2.30.2.2`.

**Why the two-pass build.** Each .NET SDK's android/ios workloads ship reference packs for the
current target framework and the previous one — the .NET 9 band builds net8 + net9, the .NET 10 band
builds net9 + net10. No single SDK builds all of them, so `BuildNugets.sh` packs twice and
[`merge-packages.py`](build/merge-packages.py) grafts the net10 assets into the net9 packages,
carrying the dependency group across with them. Both binding repositories do the same.

### Layout

| Path | What |
| --- | --- |
| `src/DatadogNet/` | The API. Shared sources declare it; `Platforms/<name>/` supplies the bodies. |
| `src/DatadogNet/Platforms/Unsupported/` | The no-op implementation the neutral `net9.0`/`net10.0` assemblies link. |
| `src/Datadog.Facade.props` | Everything the four projects share, so each `.csproj` is its identity and its dependencies. |
| `build/packages.tsv` | The package set. The build script, both workflows, the tests and the device runners all read it. |
| `tests/DatadogNet.PackageTests/` | Asserts the shape of the packed `.nupkg`s. |
| `tests/DatadogNet.DeviceTests/` | **One** app, two heads, one shared list of checks — run on an Android emulator and an iOS simulator against the packed packages. |
| `samples/DatadogNet.Maui.Sample/` | A MAUI app doing the same, the way an app would. |

The tests consume the packed `.nupkg`s from `artifacts/` rather than referencing the projects, so
they exercise what actually gets published — including the dependencies on `DatadogNet.iOS` and
`DatadogNet.Android`, which a `ProjectReference` would bypass entirely.

That the device tests are **one** file of checks running on both platforms is the point of the
repository stated as a test: if the façade did not actually unify the two SDKs, that file could not
compile for both heads.

---

## Building locally

Requires macOS, Xcode, and the .NET 9 and .NET 10 SDKs with the `android`, `ios` and `maui`
workloads, plus the Android SDK with platforms 34, 35 and 36. The .NET 9 band builds net8 and net9;
the .NET 10 band builds net10.

```bash
./build/BuildNugets.sh
dotnet test tests/DatadogNet.PackageTests
```

> **Until `DatadogNet.iOS 2.30.2.2` is on nuget.org**, restore needs it in the local `artifacts/`
> feed: build it from the `maui-facade-improvements` branch of that repository and copy the packages
> across. It is pinned because 2.30.2.1 cannot trace at all — see
> [`docs/upstream-changes.md`](docs/upstream-changes.md).

Run the device checks against the packed packages:

```bash
./.github/scripts/run-simulator-tests.sh 2.30.2.1 net8.0-ios18.0
```

```bash
./.github/scripts/run-emulator-tests.sh 2.30.2.1 net8.0-android34.0
```

Any of the six platform target frameworks works. CI runs net8 and net10 — the oldest asset set, and
the one the merge step produces — and skips net9, which shares a pack pass with net8.

Build and run the sample:

```bash
cd /tmp && dotnet new globaljson --sdk-version 10.0.301 --force
dotnet build <repo>/samples/DatadogNet.Maui.Sample/DatadogNet.Maui.Sample.csproj -f net10.0-ios26.0 -t:Run
```

> The sample targets the net10 band only, and this repository's `global.json` pins .NET 9 — hence
> the scratch directory. That is a constraint on the MAUI version you build the *sample* with, not
> on what the packages support: MAUI 9 cannot build against the AndroidX generation the Datadog
> Android bindings depend on, and it is an SDK defect rather than anything the bindings do.

> Anything targeting `net10.0-ios26.0` needs an Xcode carrying the iOS 26 SDK; .NET for iOS refuses
> a mismatch outright. CI handles this with the
> [`select-xcode`](.github/actions/select-xcode/action.yml) action; locally, prefix the command with
> `DEVELOPER_DIR=/Applications/Xcode_26.0.1.app/Contents/Developer`.

---

## Upgrading

The two platform package sets move independently, and so does this one.

1. Bump `DatadogNetiOSVersion` and/or `DatadogNetAndroidVersion` in
   [`Directory.Build.props`](Directory.Build.props), along with `DatadogNativeVersion` and
   `DatadogAndroidNativeVersion` beside them.
2. Reset `DatadogBindingRevision` to `1` if either native version moved; increment it if neither did.
3. `./build/BuildNugets.sh` and run both test suites.
4. Re-read [`docs/native-surface.md`](docs/native-surface.md) against the new SDKs. A new setting on
   one platform is reachable through `ConfigureNative` on day one; lifting it into the façade is a
   separate decision, and one worth taking only when the *other* platform has an equivalent.

A minor version of either SDK has repeatedly added members without removing any, so the usual upgrade
is a two-line edit and a test run. The exception is a major: see the note on 3.x above.

---

## Releasing

Tag it. `v2.30.2.1` builds, tests, publishes all four packages to nuget.org via trusted publishing,
and creates a GitHub release.

Pull requests publish a `-beta.<pr>.<run>` prerelease of the whole set.

Both run the same [`build.yml`](.github/workflows/build.yml): pack, package tests, the simulator and
emulator checks, and — on pull requests — a compile of the MAUI sample against the packed packages.

Curated notes in `docs/release-notes/<version>.md` replace the generated commit list when present.

---

## Troubleshooting

**Nothing appears in Datadog.** Check `Site` matches your organisation's region — this is the most
common cause by a wide margin. Then set `Verbosity = DatadogVerbosity.Debug` in the configuration and
watch logcat or the Xcode console; that is where "your client token is invalid" appears.

**Everything is a no-op and `Datadog.IsSupported` is `false`.** You are on a Windows or Mac Catalyst
head, or in a unit test running against the neutral assembly. That is what it is for.

**RUM records one view for the whole app.** Page tracking is off, or the app never reaches
`Application.PageAppearing` — a Blazor Hybrid app, typically, where one MAUI page hosts every route.
Report views yourself with `Datadog.Rum.StartView` from your router.

**HTTP calls do not appear as RUM resources.** The client has no `DatadogHttpMessageHandler`. Neither
SDK's automatic instrumentation sees `HttpClient`; see [HTTP](#http).

**Traces do not continue into my backend.** The host is not in `FirstPartyHosts`, which is an
allowlist and matched by suffix on a label boundary — `example.com` covers `api.example.com` but not
`notexample.com`.

**`NU1608` about `Xamarin.AndroidX.SavedState` or `Lifecycle.Runtime`.** A property of the AndroidX
graph the Datadog Android bindings pull in: two packages disagree about a sibling's exact range, and
NuGet resolves the higher version and warns. The result works — it is what the emulator checks run
against. Add `<NoWarn>$(NoWarn);NU1608</NoWarn>` or pin the versions yourself.

**A framework fails to load, or `NoClassDefFoundError`.** Usually a partially restored package. Clear
the cached copies and restore again: `rm -rf ~/.nuget/packages/datadognet*`.

**`'Trace' is an ambiguous reference`, or `'Logs' does not exist in the namespace`.** These are
`DatadogNet.Android` symptoms, from calling the bindings directly. The façade puts everything on
`Datadog.*` precisely to avoid them.

---

## Licence

The code in this repository is [MIT](LICENSE). It depends on `DatadogNet.iOS` and
`DatadogNet.Android`, whose packages ship native binaries built and published by Datadog under
Apache-2.0. Every package here declares `MIT AND Apache-2.0` and carries both texts under
`licenses/`.
