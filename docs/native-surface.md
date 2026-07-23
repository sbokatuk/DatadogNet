# What the façade covers, and what it leaves to `ConfigureNative`

Measured against **dd-sdk-ios 2.30.2** and **dd-sdk-android 2.26.3** specifically, by reading the
bound API surface of `DatadogNet.iOS 2.30.2.1` and `DatadogNet.Android 2.26.3.1` rather than the
documentation.

That distinction matters more than it sounds. Datadog's documentation describes the current SDKs, and
several things it documents are **not in these versions** — `trackMemoryWarnings` and
`trackResourceHeaders` are documented for iOS and appear nowhere in 2.30.2's bound surface;
`trackResourceHeaders` likewise for Android 2.26.3. Anything below is what the pinned versions
actually have.

The rule for lifting something into the façade is simple: **a setting is lifted when both platforms
have an equivalent, and left to `ConfigureNative` when only one does.** A cross-platform property
that silently does nothing on one platform is worse than a platform conditional that says so.

---

## Core configuration

| Native | Façade |
| --- | --- |
| `clientToken`, `env`, `service` | `DatadogConfiguration.ClientToken` / `.Env` / `.Service` |
| `site` / `useSite` | `.Site` — the seven sites both declare |
| `trackingConsent` | `.TrackingConsent`, and `Datadog.SetTrackingConsent` |
| `batchSize`, `uploadFrequency`, `batchProcessingLevel` | `.BatchSize`, `.UploadFrequency`, `.BatchProcessingLevel` |
| `verbosityLevel` / `Datadog.verbosity` | `.Verbosity`, and `Datadog.Verbosity` |
| `additionalConfiguration` | `.AdditionalConfiguration` |
| `setFirstPartyHostsWithHeaderType` (Android) | `.FirstPartyHosts` — see the note below |
| `setCrashReportsEnabled` (Android) | `.CrashReportsEnabled`, documented as Android-only |
| `variant` (Android) | `.Variant`, documented as Android-only |

**Left to `ConfigureNative`:** `setEncryption`, `setServerDateProvider`, `proxyConfiguration`,
`bundle`, `backgroundTasksEnabled` (iOS); `setBackpressureStrategy`,
`setPersistenceStrategyFactory`, `setUploadSchedulerStrategy`, `setUseDeveloperModeWhenDebuggable`,
`setProxy` (Android). Each exists on one platform only, or takes a native type — an
`okhttp3.Authenticator`, an `NSBundle` — that has no cross-platform form.

**`DatadogSite.Staging`** is deliberately absent. dd-sdk-android has it; dd-sdk-ios does not. It is
Datadog's own internal environment.

**`FirstPartyHosts` is read by this façade, not only passed down.** On Android it is also set on the
native builder, where it feeds `DatadogInterceptor`. On iOS it has no configuration-level counterpart
at all — first-party hosts live on `DDRUMURLSessionTracking`, which only applies to an
`NSURLSession` the SDK has instrumented. `DatadogHttpMessageHandler` reads the list from the
configuration and injects in the managed pipeline, which is what makes the setting behave the same
on both.

## RUM

| Native | Façade |
| --- | --- |
| `startView` / `stopView` (by key) | `IRumMonitor.StartView` returning a disposable scope, `StopView` |
| `addAction`, `startAction`, `stopAction` | same names |
| `addError` (message and error forms) | `AddError(Exception)`, `AddError(string, …)` |
| `startResource`, `stopResource`, `stopResourceWithError` | same names, with nullable `int`/`long` |
| `addTiming` | `AddTiming` |
| `addFeatureFlagEvaluation` | `AddFeatureFlagEvaluation` |
| `addAttribute`, `removeAttribute` | same, plus `AddAttributes`/`RemoveAttributes` |
| `stopSession` | `StopSession` |
| `currentSessionID` / `getCurrentSessionId` | `GetCurrentSessionIdAsync` |
| `debug` | `Debug` |
| `sessionSampleRate`, `telemetrySampleRate` | `RumOptions.SessionSampleRate`, `.TelemetrySampleRate` |
| `trackFrustrations`, `trackBackgroundEvents`, `trackAnonymousUser` | same names |
| `vitalsUpdateFrequency` | `.VitalsUpdateFrequency` |
| `longTaskThreshold` / `trackLongTasks` | `.LongTaskThreshold`, a `TimeSpan?` |
| UIKit predicates (iOS) / `trackUserInteractions` + view strategy (Android) | `.TrackAutomaticInstrumentation` |
| `customEndpoint` | `.CustomEndpoint` |

**Left to `ConfigureNative`:**

- **iOS only** — `trackWatchdogTerminations`, `appHangThreshold`, `swiftUIViewsPredicate`,
  `swiftUIActionsPredicate`, `onSessionStart`, `setURLSessionTracking`, and a custom
  `uiKitViewsPredicate`/`uiKitActionsPredicate`.
- **Android only** — `trackNonFatalAnrs`, `collectAccessibility`, `setSlowFramesConfiguration`,
  `setSessionListener`, `setInitialResourceIdentifier`, `setLastInteractionIdentifier`, and the
  `Fragment`/`Mixed`/`Navigation` view-tracking strategies.
- **Both, not lifted** — the five event mappers. See the README.

**Not exposed at all:** `startView(viewController:)` on iOS and `startView(Object)` on Android take a
platform view object as the key. A MAUI app has neither, and the key-based form is what
`DatadogNet.Maui` uses.

Two enums are narrower than one platform's:

- `RumActionType` has the four both declare. Android also has `Click` and `Back`.
- `RumErrorSource` has the five both declare. Android also has `Agent`, `Logger` and `Report`, which
  the SDK sets for itself.

## Logs

| Native | Façade |
| --- | --- |
| `Logs.enable` | `LogsOptions` on the configuration |
| `Logger.create` / `Logger.Builder` | `IDatadogLogs.CreateLogger(LoggerOptions)` |
| the six level methods | `IDatadogLogger.Log(level, …)` plus six shorthands |
| `addAttribute`, `removeAttribute` (logger and feature) | same on `IDatadogLogger` and `IDatadogLogs` |
| `addTag`, `removeTagsWithKey` | `AddTag`, `RemoveTagsWithKey` |
| `customEndpoint` | `LogsOptions.CustomEndpoint` |

**Left to `ConfigureNative`:** the log event mapper.

`DatadogLogLevel` is Datadog's six levels rather than either platform's log priorities. On Android the
SDK takes an `android.util.Log` constant, and there is no `Notice` — the façade maps it to `INFO`,
which is what dd-sdk-android's own `Logger` records. Android's `Verbose` has no iOS counterpart and is
not exposed; it would be a level that silently meant something different on each platform.

**Not exposed:** `Logger.addTag(String)` and `removeTag(String)` — the tag-without-a-key forms. Only
Android has them.

## Trace

| Native | Façade |
| --- | --- |
| `Trace.enable` | `TraceOptions` on the configuration |
| `DDTracer.shared` / `GlobalTracer.get()` | `Datadog.Tracer` |
| `startSpan` | `IDatadogTracer.StartSpan` |
| `setTag` (string, number, bool) | `IDatadogSpan.SetTag` ×3 |
| `setErrorWithKind` (iOS) / the error log-field convention (Android) | `SetError(Exception)`, `SetError(kind, …)` |
| `log` | `Log(dictionary)` |
| `setActive` (iOS) / `activateSpan` (Android) | `Activate()` returning a scope |
| `finish` | `Finish()`, and `Dispose()` |
| `inject` | `IDatadogTracer.Inject` returning headers |
| `sampleRate`, `service`, `networkInfoEnabled`, `bundleWithRumEnabled` | `TraceOptions` |
| `tags` / `addTag` | `.GlobalTags` |
| `setTracingHeaderTypes` (Android) / the writer types (iOS) | `.HeaderTypes` |

Both SDKs are OpenTracing-shaped in 2.x, which is why this is one interface. The differences are
below the API:

- **Trace and span ids.** Android has `SpanContext.toTraceId()`/`toSpanId()`. iOS has nothing — the
  ids are parsed back out of an injected Datadog-format header.
- **Header formats.** Android's are a property of the tracer, so one `inject` writes all of them.
  iOS needs one writer object per format.
- **`SetError`.** iOS has `setErrorWithKind`. `io.opentracing.Span` has no equivalent, so on Android
  it is an `error` tag plus four specific log fields.
- **Activation.** Android has a real scope manager and `Scope.close()`. On iOS `setActive` pushes and
  `finish` pops, with no explicit deactivate.

**Left to `ConfigureNative`:** the span event mapper, `DDTraceURLSessionTracking` (iOS), and
`setPartialFlushThreshold` (Android).

**Not exposed:** baggage items (`setBaggageItem`/`getBaggageItem`). Present on both, but deprecated in
OpenTracing and a footgun — baggage propagates to every downstream service, so anything put in it
leaves the app.

## Session Replay

| Native | Façade |
| --- | --- |
| `SessionReplay.enable` | `SessionReplayOptions` on the configuration |
| `replaySampleRate` | `.SampleRate` |
| `textAndInputPrivacyLevel` | `.TextAndInputPrivacy` — same three values, same names |
| `imagePrivacyLevel` | `.ImagePrivacy` — see below |
| `touchPrivacyLevel` | `.TouchPrivacy` |
| `startRecordingImmediately` | `.StartRecordingImmediately` |
| `startRecording` / `stopRecording` | `ISessionReplay.StartRecording` / `StopRecording` |
| `customEndpoint` | `.CustomEndpoint` |
| `addExtensionSupport(MaterialExtensionSupport())` | applied automatically on Android |

`ImagePrivacy.MaskContentImages` is the one setting whose behaviour differs: iOS masks images not
bundled with the app, Android masks images above roughly 100×100 dp. Same intent, different rule.

The Material extension is registered for you because MAUI's Android handlers are built on Material
Components, and without it a MAUI app records as a screen of blank boxes. The Compose extension is
not, because nothing in MAUI draws Compose — add `DatadogNet.SessionReplayCompose.Android` and
register it through `ConfigureNative` if you host Compose content.

**Left to `ConfigureNative`:** per-view privacy overrides (iOS `DDSessionReplayPrivacyOverrides`, a
`UIView` category), `setDynamicOptimizationEnabled` and `setSystemRequirements` (Android), the
deprecated single `defaultPrivacyLevel`, and `featureFlags`.

**Not bindable at all:** custom Session Replay wireframe mappers on Android. The hierarchy is generic
and erases to `map(Object, …)`, which the generator cannot bind — see `DatadogNet.Android`'s README.
The types still ship and still run, so recording is unaffected; what you cannot do is author a custom
mapper in C#.

## Identity and consent

| Native | Façade |
| --- | --- |
| `setUserInfo`, `addUserExtraInfo` / `addUserProperties`, `clearUserInfo` | `Datadog.SetUser`, `.AddUserExtraInfo`, `.ClearUser` |
| `setAccountInfo`, `addAccountExtraInfo`, `clearAccountInfo` | `Datadog.SetAccount`, `.AddAccountExtraInfo`, `.ClearAccount` |
| `setTrackingConsent` | `Datadog.SetTrackingConsent` |
| `clearAllData` | `Datadog.ClearAllData` |
| `stopInstance` | `Datadog.Stop` |
| `isInitialized` | `Datadog.IsInitialized` |

**Not exposed:** named SDK instances. Both SDKs support several cores side by side —
`Datadog.initialize(instanceName, …)` on Android, and the `SDKCore` parameter throughout. No MAUI app
has been observed to want two, and threading an instance name through every call would cost every app
something to serve none.

## Crash reporting and web views

Separate packages, and each is a single call: `CrashReporting.Enable()` and
`DatadogWebViewTracking.Enable(platformWebView, hosts, logsSampleRate)`.

What they do differs by platform, and the README's platform-differences table says how. The one that
catches people out: on Android `DatadogWebViewTracking.Disable` is a no-op, because dd-sdk-android has
no disable — its bridge is a `JavascriptInterface` attached to the `WebView` and goes when the
`WebView` does. iOS needs an explicit teardown, because its bridge is a `WKUserContentController`
script message handler, which `WKWebView` retains strongly and which therefore outlives the page.
