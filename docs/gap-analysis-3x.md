# Coverage, issues and improvements across the three repositories — 3.x

Everything below is measured, not recalled: the iOS numbers come from diffing each framework's
`-Swift.h` and `.swiftinterface` against the committed `ApiDefinitions.cs`; the Android numbers from
`api.xml`, the committed `Transforms/Metadata.xml`, and Maven Central's artifact list.

**Short version.** Both bindings cover their native surface essentially completely. What is missing
is missing *upstream* — Datadog has not projected it into a form a binding can reach. The real work
is in the façade, and the sharpest single defect is `IDatadogSpan.TraceId` returning a different
format on each platform.

---

## 1. iOS binding — `DatadogNet.iOS 3.14.0.1`

### Coverage is complete

Type-by-type against the Objective-C headers:

| Framework | ObjC types exposed | Bound | Not bound |
| --- | ---: | ---: | --- |
| `DatadogRUM` | 377 | 377 | — |
| `DatadogLogs` | 16 | 16 | — |
| `DatadogCore` | 12 | 12 | — |
| `DatadogTrace` | 12 | 12 | — |
| `DatadogSessionReplay` | 4 | 4 | — |
| `DatadogInternal` | 2 | 2 | — |
| `DatadogCrashReporting` | 1 | 1 | — |
| `DatadogWebViewTracking` | 1 | 1 | — |

Member-by-member is the same story — of 61 selectors and properties on `DatadogCore`, 59 are
exported; the two that are not are `init` and `new`, which `[DisableDefaultCtor]` removes on purpose.
`DatadogLogs` additionally skips one `_internal_sync_…` member. Trace and Session Replay are clean.

**There is nothing to add to this binding.** Its gaps are upstream's.

### What is missing, and why the binding cannot fix it

**Feature Flags is entirely unreachable.** `DatadogFlags` ships a real 15-type Swift API —
`Flags`, `FlagsClientProtocol`, `FlagDetails<T>`, `FlagSnapshot`, `FlagsEvaluationContext`,
`FlagsStateListener` and friends — and exposes **zero** Objective-C types. It leans on Swift
generics (`FlagDetails<T>`), which is precisely why it has no ObjC projection and why no amount of
binding work reaches it.

**Profiling likewise** — 2 Swift types, 0 ObjC.

**`OpenTelemetryApi` has no `-Swift.h` at all.** It is a pure-Swift module. `DatadogNet.OpenTelemetryApi.iOS`
can only ever be a link-time dependency of `DatadogNet.Trace.iOS`; there is no API in it to call, now
or later.

**Half of `DatadogTrace` is Swift-only.** 24 public Swift types, 12 projected. The casualty that
matters is `OTelTracerProvider` — the OpenTelemetry tracing path, which Datadog now documents as the
preferred one on iOS. From C# you get OpenTracing (`OTTracer`/`OTSpan`) and nothing else.

### Improvements worth making

1. **Restore the "API coverage" section to the README.** The 2.x README had one and it was the most
   trustworthy thing in the document; the 3.x README dropped it. The numbers above are one script
   away, and stating "Flags and Profiling have no ObjC surface — 15 and 2 Swift types respectively,
   0 projected" is far stronger than "no C# API yet".
2. **Make `DatadogAttributes.ToNSObject` public.** Still private in 3.x. Every consumer calling
   `AddAttributeForKey`, `AddViewAttributeForKey` or `AddFeatureFlagEvaluationWithName` either
   hand-wraps the value or round-trips a one-element dictionary. The façade does the latter today.
3. **Add the tracing helpers** the façade currently shims: `GetTraceId()`/`GetSpanId()` (the only
   route to the ids is injecting into a Datadog headers writer and parsing), `InjectHeaders()` (the
   writer-is-also-the-carrier dance, one writer type per format), `SetError(Exception)` and
   `Log(IReadOnlyDictionary)`.
4. **Add `GetCurrentSessionIdAsync()`** on `DDRUMMonitor`, and single-value `AddAttribute` /
   `AddFeatureFlagEvaluation` overloads.
5. **Consider dropping `DatadogNet.Flags.iOS` and `DatadogNet.Profiling.iOS`.** They ship a native
   payload that no C# code can call. Keeping them mirrors the SDK, which is a real argument — but a
   package a consumer can only waste bytes on deserves a louder warning than it has, or an
   `IsPackable=false` until upstream projects the API.

---

## 2. Android binding — `DatadogNet.Android 3.12.1.1`

### Coverage is tight and well-argued

Only **five** `remove-node` rules across the whole 13-package set, each with a written justification:

| Package | Removed | Why it does not matter |
| --- | --- | --- |
| SessionReplay | the entire `…recorder.mapper` package | Generic hierarchy erasing to `map(Object, …)`; the types still ship and still run, so recording is unaffected. You cannot author a custom mapper in C#. |
| Core | `FlushableExecutorService`'s `ExecutorService` members | Cannot supply a custom executor from C#. No MAUI equivalent. |
| Trace | `SessionRebasedSampler` | Internal; sampling is configured through the builder. |
| Internal | `EvictingQueue` | Internal buffering; referenced by no bound member. |
| OkHttp | `TracingInterceptor.BaseBuilder.build`, `OkHttpRequestInfoBuilder` | Generator limits on covariant returns; both still ship. |

That is a better record than most bindings manage, and the rules are self-documenting.

### What is missing: fourteen unbound artifacts

Datadog publishes **27** `dd-sdk-android*` artifacts. Thirteen are bound. The rest:

| Artifact | Verdict for a MAUI app |
| --- | --- |
| `-compose` | **Worth considering.** RUM auto-instrumentation for Jetpack Compose — distinct from `session-replay-compose`, which is already bound. Irrelevant to MAUI itself, relevant to a hybrid app hosting Compose. |
| `-tv` | **Worth considering** if Android TV is ever a target. |
| `-trace-otel`, `-okhttp-otel` | OpenTelemetry interop. Interesting only if the OTel path becomes the recommended one, as it has on iOS. |
| `-rum-coroutines`, `-trace-coroutines`, `-rx`, `-ktx` | Kotlin-ecosystem. Coroutines and Rx have no C# meaning. Correctly skipped. |
| `-coil`, `-fresco`, `-glide`, `-sqldelight`, `-timber` | Instrument Kotlin/Java libraries a MAUI app does not use. Correctly skipped. |
| `-gradle-plugin`, `-benchmark-internal`, `dd-sdk-android` (BOM) | Not bindable / not a library. |

**None of these is a defect** — but nothing in the repository says they were considered. `packages.tsv`
lists what is bound and is silent on what is not.

### The Kotlin boundary

67 `*Kt` classes appear in `api.xml` — the synthetic containers Kotlin extension functions land in.
Most are internal (`ThreadExtKt`, `FileExtKt`). A few are public API, and the useful one is
`SpanExtKt.withinSpan(String, DatadogSpan, boolean, Function1<DatadogSpan, T>)`: "run this block
inside a span". It is reachable, but its lambda parameter is a Kotlin `Function1`, so calling it from
C# needs a `Java.Lang.Object` subclass. The façade's `using var span = …` covers the same ground more
idiomatically, so this is a gap on paper only.

### Improvements worth making

1. **The same four convenience additions** the 2.x branch got and 3.x has not: public
   `DatadogAttributes.ToJava`, `GetCurrentSessionIdAsync` (still a `kotlin.jvm.functions.Function1`,
   still inexpressible as a C# lambda), single-value `AddAttribute`/`AddFeatureFlagEvaluation`, and
   `Logger.AddAttribute`.
2. **A tracing injection helper.** `DatadogPropagation.Inject` takes a Kotlin
   `(C, String, String) -> Unit` — `IFunction3`, no C# lambda form. Every consumer who wants
   distributed tracing has to write the adapter the façade already carries. This is the single
   highest-value addition on the Android side.
3. **Document the unbound artifacts** in `packages.tsv`, in the same voice the `remove-node` rules
   use. "Not bound, because it instruments Glide" is a complete answer; silence is not.
4. **Consider `-compose` and `-tv`** if either audience is real.

---

## 3. The façade — `DatadogNet 3.14.0.1`

This is where the actionable defects are.

### Issues

**`IDatadogSpan.TraceId` returned a different format on each platform.** *Fixed in 3.14.0.2.*

Measured, from the same check on the same commit:

```
iOS      trace 6096355397431041644                  span 1339032811858280360
Android  trace 6a61e4ff000000002e430f579ece9a6c     span 7961859199427953515
```

Not merely a different *format* — a different **width**, and one of them lossy. It is worse than
cosmetic, because `DatadogHttpMessageHandler` writes `_dd.trace_id` onto every RUM resource and that
attribute is what links a RUM resource to its APM trace.

There is a correct answer, and it is not a matter of taste. Decompiling dd-sdk-android's own
`DatadogInterceptor` — the reference implementation of exactly this correlation — settles it:

```
_dd.trace_id  ->  DatadogTraceId.toHexString()   // 32 lowercase hex, always
_dd.span_id   ->  String.valueOf(long)           // decimal
```

The asymmetry is the wire format rather than an oversight. And `toHexString()` is
`toHexStringPadded(…, 32)` on **both** `DD128bTraceId` and `DD64bTraceId`, so a 64-bit id is the low
half with sixteen leading zeros — never a 16-character string.

So **Android was right all along** and iOS was the one that was wrong. The fix was iOS-only:
reassemble the 128-bit id from the two places the Datadog headers split it across — the decimal low
half in `x-datadog-trace-id`, and the high half as `_dd.p.tid` inside `x-datadog-tags`. That is now
`TraceIdentifiers`, in shared code so it is testable off-device.

Verified on the simulator: iOS reports `6a61ec6d00000000a25ebf9b34ae45d4`.

**The device checks asserted liveness, not correctness.** *Partly addressed in 3.14.0.2.* Twenty
checks proved no call throws. Almost none asserted what was *recorded* — the trace-id bug passed
twenty green runs against `Assert(span.TraceId.Length > 0)`. The trace check now asserts the shape,
that the id's low half matches the header the SDK emitted, and that it agrees with `traceparent` on
all 128 bits — an independent second opinion from the SDK's own W3C writer. The same tightening is
still owed to attribute round-tripping and `IsEnabled` transitions.

**There were no unit tests at all.** *Fixed in 3.14.0.2.* `DatadogNet.UnitTests` now covers
`TraceIdentifiers`, `DatadogHttpMessageHandler.IsFirstParty` — the suffix-on-a-label-boundary logic
deciding whether your trace ids leak to a third party — `ActiveSpanTracker`, and the validation in
`Datadog.Initialize`. 66 tests against the neutral `net9.0` assembly, no device, ~15 ms.

**Four shims duplicate logic that belongs upstream**, listed in §1 and §2. Each is commented with
the member that would replace it, but they are still four places where the façade reimplements a
conversion the binding already knows how to do.

### Missing, and worth adding

| | Both platforms have it? | Note |
| --- | --- | --- |
| **View-scoped attributes** (`AddViewAttributes`/`RemoveViewAttributes`) | iOS yes; Android to confirm | The most valuable 3.x addition. Attributes set on a view propagate to the actions, resources and errors inside it, which removes the reason to repeat them on every call. Belongs on `IRumViewScope`. |
| `ReportAppFullyDisplayed()` | iOS yes; Android to confirm | Time-to-interactive. |
| `AddViewLoadingTime(bool overwrite)` | iOS yes; Android had it in 2.x | Same. |
| `DatadogSite.Uk1` | Android only | Cannot lift. |
| Feature operations | iOS only | Cannot lift. |
| `TrackMemoryWarnings` | iOS only | Cannot lift. |

### Improvements worth making, in order

1. ~~**Fix the trace-id format**, and tighten the check that should have caught it.~~ *Done — 3.14.0.2.*
2. ~~**Add a unit-test project** against the neutral assembly.~~ *Done — 66 tests.*
3. **Lift view-scoped attributes onto `IRumViewScope`** once Android's equivalent is confirmed. This
   is a shape change, which is why it was held back from the 3.x release rather than bolted on.
4. **Push the four shims upstream** and delete them here.
5. **Reconsider event mappers.** Currently `ConfigureNative`-only, on the argument that the two event
   models are large and unrelated. That argument is sound for the full models — but the one thing
   most apps want is "drop or redact this field", and a narrow cross-platform hook over the two or
   three fields both models share (message, view URL, resource URL) would serve most of the need
   without inventing a third schema.
6. **`DatadogNet.Maui` cannot ship a Windows or Mac Catalyst head**, so a multi-headed app still
   guards one reference. Nothing to do until MAUI ships a platform-neutral reference assembly, but it
   is the last conditional left in a repository whose selling point is not needing them.

---

## What I would do first

1. ~~The **trace-id format bug**~~ — *done in 3.14.0.2, verified on the simulator.*
2. ~~The **unit-test project**~~ — *done: 66 tests, ~15 ms, no device.*
3. The **Android injection helper** upstream — the highest-value binding addition on either side.
4. **View-scoped attributes** — the best thing 3.x added that the façade does not yet expose.

On reaching the unreachable: the Swift-only iOS features are not as unreachable as this document
first said. A hand-written `@objc` shim can project them, and a compiling prototype for Flags now
exists — see [`swift-interop-plan.md`](swift-interop-plan.md), which recommends doing it for Flags
and Profiling and explicitly not for OpenTelemetry.
