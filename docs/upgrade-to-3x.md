# Upgrading the façade from the 2.x line to 3.x

Measured against **dd-sdk-ios 3.14.0** (`DatadogNet.iOS 3.14.0.1`) and **dd-sdk-android 3.12.1**
(`DatadogNet.Android 3.12.1.1`), by reading the bound API surface rather than the documentation.

The headline: **the façade's public API barely moves; both platform implementations are rewritten.**
That is the façade earning its keep — an app that used `Datadog.Rum`, `Datadog.Logger` and
`Datadog.Tracer` against 2.x keeps its call sites, while underneath one SDK renamed every tracing
type and the other redistributed its whole Objective-C surface across ten namespaces.

---

## 1. What we added to the 2.x bindings, and whether 3.x still needs it

None of the `2.26.3.2` / `2.30.2.2` convenience additions are on the 3.x branches. Each has to be
re-decided rather than merely re-applied.

| 2.x addition | Still needed in 3.x? |
| --- | --- |
| **iOS** `ApiDefinitions`: `IOTTracer`/`IOTSpan`/`IOTSpanContext` instead of the protocol classes | **Already done.** The 3.x binding declares the interface forms and the forward declarations. The bug that made 2.x tracing unusable does not exist here. |
| **iOS** `DatadogAttributes.ToNSObject` public | **Yes.** Still private, and 3.x adds *more* single-value members (`AddViewAttributeForKey`). |
| **iOS** `TracingExtensions`: `GetTraceId`/`GetSpanId` | **Yes.** `OTSpanContext` still exposes nothing but `forEachBaggageItem`; the ids are still only reachable by injecting into a Datadog headers writer and parsing. |
| **iOS** `TracingExtensions`: `InjectHeaders` | **Yes, and simpler.** The 3.x writers take no sampling argument — sampling is derived from the RUM `session.id` — so the awkward `DDTraceSamplingStrategy` argument is gone. |
| **iOS** `TracingExtensions`: `SetError(Exception)`, `Log(dictionary)` | **Yes.** `OTSpan` is unchanged. |
| **iOS** `DDRUMMonitor`: `AddAttribute`, `AddAttributes`, `AddFeatureFlagEvaluation`, `GetCurrentSessionIdAsync` | **Yes**, plus new siblings for the 3.x view-attribute members. |
| **iOS** `DDLogs`/`DDLogger.AddAttribute` | **Yes.** |
| **Android** `DatadogAttributes.ToJava` public | **Yes.** |
| **Android** `RumMonitorExtensions.GetCurrentSessionIdAsync` | **Yes.** Still a `kotlin.jvm.functions.Function1`. |
| **Android** `RumMonitorExtensions` resource overloads, `AddAttribute`, `AddFeatureFlagEvaluation` | **Yes.** |
| **Android** `StopResourceWithError` non-null `stackTrace` guard | **Needs re-checking** against the 3.x signature before being carried over. |
| **Android** `LoggerExtensions.AddAttribute` | **Yes.** |
| **Android** `TracingExtensions`: `SetError`, `Log`, `InjectHeaders` | **Rewritten, not ported.** `io.opentracing` is gone. `DatadogSpan` has real `setError`/`setErrorMessage`/`addThrowable`/`logAttributes`, so only the injection helper is still needed — and for a different reason (see below). |
| **Android** README: minimum API level is 23 | **Moot.** dd-sdk-android 3.0 declares `minSdk 23` itself, so the manifest merger and the `.aar` agree. |

---

## 2. iOS: the same API, redistributed

**`DatadogObjc` is gone as a real framework.** In 2.x it re-exported the whole SDK, so the façade
took one package reference and one `using`. In 3.x the `DD*` types live in the module they belong
to, and `DatadogNet.Objc.iOS` survives only as an empty compatibility meta-package that redirects an
existing 2.x `PackageReference`. The façade must not use it.

| | 2.x | 3.x |
| --- | --- | --- |
| Namespaces | `DatadogObjc` for everything | `DatadogCore`, `DatadogRUM`, `DatadogLogs`, `DatadogTrace`, `DatadogSessionReplay`, `DatadogCrashReporting`, `DatadogWebViewTracking`, `DatadogInternal` |
| Package references | 1 (`Objc`) | 6 (`Core`, `RUM`, `Logs`, `Trace`, `SessionReplay`, plus `CrashReporting`/`WebViewTracking` in their own façade packages) |
| `DDSite.Us1`, `DDTrackingConsent.Granted` | properties | **methods** — `DDSite.Us1()`, `DDTrackingConsent.Granted()` |
| `DDRUMMonitor.Shared` | property | **method** — `Shared()`, plus `SharedWithInstanceName` |
| Verbosity | `DDDatadog.VerbosityLevel` property, `DDSDKVerbosityLevel` | `DDDatadog.SetVerbosityLevel(DDCoreLoggerLevel)` from `DatadogInternal` |
| Crash engine | PLCrashReporter, in its own `CrashReporter` package | KSCrash, inside `DatadogCrashReporting`; **no separate package** |
| Tracing | OpenTracing, protocols bound as classes (**broken**) | OpenTracing, protocols bound as interfaces (**works**) |
| Header writers | took `DDTraceSamplingStrategy` + `DDTraceContextInjection` | take neither; sampling follows the RUM session |

**New in 3.x that the façade can surface:**

- `TrackMemoryWarnings` on `DDRUMConfiguration` — and Android has no counterpart, so it stays behind
  `ConfigureNative`.
- **View-scoped attributes** — `AddViewAttributes`, `AddViewAttributeForKey`,
  `RemoveViewAttributeForKey`, `RemoveViewAttributesForKeys`. Attributes set on a view propagate to
  the actions, resources and errors recorded inside it, which removes the main reason to repeat
  attributes on every call.
- `ReportAppFullyDisplayed()`, `AddViewLoadingTimeWithOverwrite(bool)` — time-to-interactive.
- **Feature operations** — `StartOperationWithName`, `SucceedOperationWithName`,
  `FailOperationWithName` and their `Feature` variants, with `DDOperationOptions` and
  `DDRUMFeatureOperationFailureReason`. A named, resumable unit of work spanning views.

**New in 3.x that the façade cannot surface:** `DatadogNet.Flags.iOS` and
`DatadogNet.Profiling.iOS` ship the frameworks but expose **no callable API** — upstream has not
projected them into Objective-C, so there is nothing to bind and nothing for the façade to call.

---

## 3. Android: tracing rewritten, everything else steady

The package set grows from 12 to 13: `OpenTracing` is gone, and the trace module splits into
`TraceApi`, `TraceInternal` and `Trace`.

| | 2.x | 3.x |
| --- | --- | --- |
| Tracer | `AndroidTracer.Builder()` → `io.opentracing.Tracer`, registered on `GlobalTracer` | `DatadogTracing.NewTracerBuilder(core)` → `DatadogTracerBuilder`, registered on `GlobalDatadogTracer` |
| Span | `io.opentracing.Span` | `DatadogSpan` |
| Span creation | `tracer.BuildSpan(name).Start()` | `tracer.BuildSpan(name)` → `DatadogSpanBuilder` → `.Start()` |
| Errors | no `setError`; four log fields by convention | **real members**: `SetError(bool)`, `SetErrorMessage`, `AddThrowable`, `LogErrorMessage` |
| Trace/span ids | `SpanContext.ToTraceId()`/`ToSpanId()` strings | `DatadogTraceId.ToHexString()`/`ToLong()`, `DatadogSpanContext.GetSpanId()` → `long` |
| Injection | `tracer.Inject(ctx, Format.Builtin.TextMapInject, carrier)` — carrier copied by the marshaller, **silently produced nothing** | `tracer.Propagate().Inject(ctx, carrier, setter)` — the setter is a **Kotlin `(C, String, String) -> Unit`**, which C# cannot express as a lambda |
| Sampling | `SetSampleRate(double)` on the tracer | `WithSampleRate(double)`, plus `SetTraceRateLimit(int)` |
| Minimum API | 21 in the `.aar`, 23 in practice | 23, declared honestly |

So the injection helper is still needed on Android, for a *different* reason: 2.x's trap was a
carrier marshalled by copy; 3.x's is a Kotlin function type that has no C# lambda form. Both end in
the same place — a request that goes out untraced.

`DatadogSpan.SetTag` now has `String`, `Number`, `boolean` and `Object` overloads, so the 2.x
`Java.Lang.Double` boxing dance for a numeric tag is gone.

---

## 4. What this means for the façade's public API

**Unchanged:** `Datadog.Initialize`, `DatadogConfiguration` and every options type, `IRumMonitor`,
`IDatadogLogs`, `IDatadogLogger`, `IDatadogTracer`, `IDatadogSpan`, `ISessionReplay`,
`DatadogHttpMessageHandler`, and all of `DatadogNet.Maui`. An app moving from `2.30.2.1` to the 3.x
façade changes a version number.

**Additions worth making**, each supported on both platforms:

| Addition | iOS | Android |
| --- | --- | --- |
| `IRumViewScope.AddAttributes` / `RemoveAttributes` — view-scoped attributes | `AddViewAttributes` | `RumMonitor.addViewAttributes` (to confirm) |
| `IRumMonitor.ReportAppFullyDisplayed()` | `ReportAppFullyDisplayed` | to confirm |
| `IRumMonitor.AddViewLoadingTime(bool overwrite)` | `AddViewLoadingTimeWithOverwrite` | `addViewLoadingTime(boolean)` — already present in 2.x |

**Deliberately not lifted:** feature operations (iOS-only until Android's equivalent is confirmed),
`TrackMemoryWarnings` (iOS-only), Flags and Profiling (no API on iOS at all). All reachable through
`ConfigureNative`.

**One package-set change.** `DatadogNet.CrashReporting`'s iOS side no longer needs the separate
`CrashReporter` package — KSCrash is inside `DatadogCrashReporting`. The façade package keeps its
name and shape; only its dependency list shrinks.

---

## 5. Order of work

1. Port the still-relevant convenience additions to both 3.x binding repos, adapted (§1).
2. Rewrite `src/DatadogNet/Platforms/iOS` against the six 3.x namespaces.
3. Rewrite `src/DatadogNet/Platforms/Android` against `DatadogTracing`/`DatadogSpan`.
4. Repoint versions, package references and docs; add the three cross-platform members from §4.
5. Run the device checks on both platforms for net8 and net10 — the same 20 checks, unchanged, which
   is the test that the public API really did survive.
