# Changes made to the platform binding repositories

Building this façade surfaced a set of gaps in
[DatadogNet.iOS](https://github.com/sbokatuk/DatadogNet.iOS) and
[DatadogNet.Android](https://github.com/sbokatuk/DatadogNet.Android). Each was fixed there rather
than worked around here, because every one of them is something a plain .NET Android or .NET iOS app
hits too — the façade only found them first, by being the first thing to call both SDKs through the
same shape.

They are **uncommitted working-tree changes** in each local repository, on top of the `v2.30.2.1`
and `v2.26.3.1` trees respectively — left uncommitted so the commit message and branch are yours to
choose.

| Repository | Change | Ships in |
| --- | --- | --- |
| iOS | **`DDTracer.Shared` was unusable** — see below | `2.30.2.2` |
| iOS | `DatadogAttributes.ToNSObject` made public | `2.30.2.2` |
| iOS | `TracingExtensions`: `InjectHeaders`, `GetTraceId`, `GetSpanId`, `SetError(Exception)`, `Log(dictionary)` | `2.30.2.2` |
| iOS | `DDRUMMonitor`: `AddAttribute`, `AddAttributes`, `AddFeatureFlagEvaluation`, `GetCurrentSessionIdAsync` | `2.30.2.2` |
| iOS | `DDLogs.AddAttribute`, `DDLogger.AddAttribute` taking a plain value | `2.30.2.2` |
| iOS | README: the binding revision was described as 2 where the version says 1 | `2.30.2.2` |
| Android | `DatadogAttributes.ToJava` made public | `2.26.3.2` |
| Android | `RumMonitorExtensions`: `GetCurrentSessionIdAsync`, `AddAttribute`, `AddFeatureFlagEvaluation`, resource overloads | `2.26.3.2` |
| Android | `TracingExtensions`: `InjectHeaders`, `SetError`, `Log(dictionary)` | `2.26.3.2` |
| Android | `LoggerExtensions.AddAttribute` taking a plain value | `2.26.3.2` |
| Android | README: the minimum API level is 23, not 21 | `2.26.3.2` |
| Android | `StopResourceWithError` overload that guards a non-null Kotlin parameter | `2.26.3.2` |

The iOS changes are **required**: without the first one there is no tracing on iOS at all, so this
repository pins `DatadogNet.iOS 2.30.2.2`. The Android changes are conveniences — the façade carries
its own equivalents and still pins `2.26.3.1`, because requiring a rebuild for ergonomics would be
gratuitous. It will pick up `2.26.3.2` at the next revision and shed them.

---

## The one that matters: iOS tracing did not work at all

`DDTracer.Shared` — the only way to reach a tracer in dd-sdk-ios 2.x — threw on first use:

```
InvalidCastException: Unable to cast object of type 'DatadogObjc.DDTracer'
                      to type 'DatadogObjc.OTTracer'
   at ObjCRuntime.Runtime.ConstructNSObject[OTTracer](…)
   at DatadogObjc.DDTracer.get_Shared()
```

`OTTracer`, `OTSpan` and `OTSpanContext` are Objective-C **protocols**. A `[Protocol]` declaration
carrying a `[BaseType]` generates two things: an `IOTTracer` interface, which types that adopt the
protocol implement, and an `OTTracer` class used as a standalone wrapper. Objective Sharpie emits
member signatures using the bare name, so `DDTracer.shared` was declared as returning the *class*.
At runtime the marshaller therefore tried to construct an `OTTracer` around a native `DDTracer`, and
refused.

Every member trafficking in the three protocols now uses the interface form. The fix is mechanical
and the comments above each member still carry the original Objective-C signature.

Two things are worth drawing out of this:

**It is invisible at compile time.** `OTTracer` is a perfectly good type; code against it builds
cleanly and fails on the first call. Nothing short of running the tracer on a device finds it, which
is why it survived into a release with the repository's stated API coverage at "every public
Objective-C type but one".

**It is what the device checks are for.** [`SmokeTests.cs`](../tests/DatadogNet.DeviceTests/SmokeTests.cs)
runs on a real simulator against the packed packages, and it found this on its first run — three
checks failing with one message, pointing at one property.

---

## The rest: things only C# runs into

None of these are bugs. They are places where a faithful projection of Kotlin or Objective-C lands
somewhere a C# caller cannot easily reach, and where writing the awkward part once in the binding is
better than every consumer writing it again.

### A public per-value attribute converter

Both repositories have a `DatadogAttributes` helper that converts a `Dictionary<string, object?>`
into the native attribute map, and both keep the per-value conversion private. But several members
take a bare value rather than a map — `addAttribute`, `addFeatureFlagEvaluation` on both sides — and
a caller then has to either hand-wrap the value, which is exactly what the helper exists to avoid, or
round-trip a one-element dictionary to get at the result. The façade did the latter for a while;
`ToJava` and `ToNSObject` are now public and it does not.

### `GetCurrentSessionIdAsync` on Android

`RumMonitor.getCurrentSessionId` takes a `kotlin.jvm.functions.Function1`, which C# cannot express as
a lambda: it binds as an interface, so calling it means declaring a `Java.Lang.Object` subclass that
implements `IFunction1` and returns `null` for Kotlin's `Unit`. That is a great deal of ceremony for
a value most apps want in order to paste it into a support ticket. dd-sdk-ios takes an ordinary block
for the same call.

### Trace and span ids on iOS

dd-sdk-ios's `OTSpanContext` declares nothing but `forEachBaggageItem` — there is no `traceID` or
`spanID` on it or on any bound type. dd-sdk-android has both directly, on `SpanContext.toTraceId()`
and `toSpanId()`. The only route to them from Objective-C is to inject into a Datadog-format headers
writer and parse what comes out, which is now `GetTraceId()`/`GetSpanId()` rather than something each
caller rediscovers.

They matter because they are what links a RUM resource to its APM trace: Datadog correlates the two
through `_dd.trace_id` and `_dd.span_id` attributes on the resource, and nothing else produces that
link.

### Header injection on both

Two different traps, one helper each.

On **iOS** the carrier passed to `inject` is also where the result is read back from, and there is a
different writer type per format, each with its own constructor arity. Get it wrong and you get no
headers and no error.

On **Android** the obvious call — `new TextMapInjectAdapter(myDictionary)` — **silently produces
nothing**. The adapter's constructor takes an `IDictionary<string, string>`, which the binding
marshals by *copying* into a fresh `java.util.HashMap`; the SDK writes the headers into the copy, the
managed dictionary never sees them, and the request goes out untraced. `InjectHeaders` implements
`ITextMapInject` instead, so the SDK calls back into managed code.

### `SetError` on an OpenTracing span

`io.opentracing.Span` has no `setError`; dd-sdk-ios's `OTSpan` does. The Datadog convention on the
Java side is an `error` tag plus four specific log fields, which dd-trace turns into the span's error
facets — and getting the field names wrong produces a span that looks fine and is never counted as an
error. Worth writing down once.

### A `String` that looks like a `String?`

`RumMonitor.stopResourceWithError` declares `stackTrace: String` — not `String?` — while `errorType`
beside it is nullable and `addErrorWithStacktrace` takes a nullable stack. The generated C# signature
shows all three as plain `string`, so nothing distinguishes them, and passing null for the wrong one
throws at runtime:

```
NullPointerException: Parameter specified as non-null is null: method
DatadogRumMonitor.stopResourceWithError, parameter stackTrace
```

The added overload takes `string?` and substitutes empty, so the C# signature says what Kotlin meant.

This one is not a binding defect — the projection is correct — but it is a case where being faithful
to Kotlin loses information a C# caller needs, and the convenience layer is the right place to put it
back.

### The Android minimum API level is 23, not 21

`DatadogNet.Android`'s README records the floor as **21**, read off the `.aar` manifests, and treats
it as a practical argument for the 2.x line over 3.0 — which raised its own floor to 23.

The argument does not survive contact with the transitive graph. Those `.aar`s depend on an AndroidX
generation in which `androidx.savedstate` **1.4.0** declares `minSdkVersion 23`, and the Android
manifest merger takes the maximum across the whole graph rather than trusting the direct dependency.
An app declaring 21 does not merely misbehave on old devices — it fails to build:

```
uses-sdk:minSdkVersion 21 cannot be smaller than version 23 declared in library
… androidx.savedstate.savedstate-android.aar
    Suggestion: … or use tools:overrideLibrary="androidx.savedstate" to force usage
                (may lead to runtime failures)
```

This façade therefore targets 23, and does **not** take the `overrideLibrary` escape: it forces the
library in and, as the error itself says, may fail at runtime instead — a worse trade for the two API
levels it buys.

Found the same way as the tracing bug: by the device checks refusing to build for a real emulator.

---

## What was left alone

**The Android `Configuration.Builder` service parameter.** Kotlin defaults it to the package name and
C# does not inherit Kotlin's defaults, so a C# caller passes all four arguments. An overload could
hide that, but the parameter is `@Nullable` and passing `null` genuinely selects the Kotlin default,
so the existing signature is not wrong — only verbose.

**Session Replay's `ImagePrivacy` middle level.** iOS masks images not bundled with the app; Android
masks images above roughly 100×100 dp. Both aim at "hide user content, keep the interface legible",
but they are different rules and no binding change can make them the same. The façade names its value
`MaskContentImages` and documents both.

**RUM event mappers.** Reachable on both platforms, and the supported way to redact events on the
device — but the event models are large, entirely different between the two SDKs, and generated from
separate schemas. A cross-platform version would be a third schema to maintain and would still not
expose the fields only one platform has. The façade routes to them through `ConfigureNative`.
