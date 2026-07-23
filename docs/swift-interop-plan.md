# Reaching the Swift-only iOS APIs from C#

`docs/gap-analysis-3x.md` records three iOS features that no binding can reach, because Datadog
never projected them into Objective-C: **Feature Flags** (15 Swift types, 0 ObjC), **Profiling**
(2 / 0), and **OpenTelemetry tracing** (`OTelTracerProvider`, plus the whole of `OpenTelemetryApi`,
which has no `-Swift.h` at all).

The question is whether we can write that projection ourselves. **Yes — and it is already proven for
Flags.** The verdict differs per target, so they are taken separately.

---

## How it works

Swift can export to Objective-C with `@objc`, and .NET's iOS binding tooling consumes Objective-C.
So a small Swift framework of our own sits between them:

```
DatadogFlags (Swift only)
        ↑ import
DatadogFlagsObjc (ours — @objc wrappers)
        ↓ -Swift.h
Objective Sharpie → ApiDefinitions.cs → DatadogNet.FlagsObjc.iOS
        ↓
DatadogNet façade → IFeatureFlags
```

Nothing in the shim adds behaviour. Every member forwards straight through. The work is entirely in
reshaping what Swift expresses and `@objc` cannot.

This is viable because the Datadog xcframeworks ship `.swiftinterface` files — they are built with
library evolution enabled, so any newer Swift compiler can `import` them. Without that, none of this
would be possible.

### What `@objc` refuses, and the answer in each case

| Swift | Reaches ObjC? | What the shim does |
| --- | --- | --- |
| `FlagDetails<T>` — generic struct | No | One `DDFlagDetails` class with `value: Any`. Collapses five instantiations into one type. |
| `AnyValue` — enum with associated values | No | Maps to the Foundation graph: `NSString`/`NSNumber`/`NSDictionary`/`NSArray`/`NSNull`. This is *better* for C#, which gets `object` and needs no further translation. |
| `FlagsClientState`, `FlagEvaluationError` — plain enums | No | Restated as `Int`-backed `@objc` enums. Lossless. |
| `FlagsEvaluationContext`, `Flags.Configuration` — structs | No | Mutable `NSObject` classes with a `var swift:` converting back. |
| `Result<Void, FlagsError>` completion | No | `(NSError?) -> Void`, which is also what a C# `Task` wants. |
| `Flags` — caseless enum as a namespace | No | `final class DDFlags` with static members. |
| `enum FlagEvaluationError?` — optional enum | No | A `none = 0` case. |
| `FlagsStateListener` protocol | Yes, if `@objc` | Wrapped in a block-based listener returning a cancellation token, so there is no way to leak a subscription. |

---

## Flags — **do it**. Prototype written and compiling.

A working prototype lives at [`shims/DatadogFlagsObjc/`](../../DatadogNet.iOS/shims/DatadogFlagsObjc/)
in the iOS binding repo. It compiles against the real 3.14.0 `DatadogFlags.xcframework` with zero
warnings and emits exactly the Objective-C surface Sharpie needs:

```objc
@interface DDFlagsClient : NSObject
@property (nonatomic, readonly) enum DDFlagsClientState currentState;
- (BOOL)boolValueForKey:(NSString *)key defaultValue:(BOOL)defaultValue;
- (NSString *)stringValueForKey:(NSString *)key defaultValue:(NSString *)defaultValue;
- (NSInteger)integerValueForKey:(NSString *)key defaultValue:(NSInteger)defaultValue;
- (double)doubleValueForKey:(NSString *)key defaultValue:(double)defaultValue;
- (id)objectValueForKey:(NSString *)key defaultValue:(id)defaultValue;
- (DDFlagDetails *)boolDetailsForKey:(NSString *)key defaultValue:(BOOL)defaultValue;
- (void)setEvaluationContextWithTargetingKey:(NSString *)targetingKey
                                  attributes:(NSDictionary<NSString *, id> *)attributes
                                  completion:(void (^)(NSError * _Nullable))completion;
- (DDFlagsStateSubscription *)addStateListener:(void (^)(enum DDFlagsClientState))handler;
- (NSDictionary<NSString *, DDFlagSnapshot *> * _Nullable)snapshot;
@end
```

Two things make Flags the easy case:

1. **Datadog already flattened the generics at the convenience layer.** `FlagsClientProtocol` has a
   generic `getDetails<T>`, but the extensions on it provide `getBooleanValue`, `getStringValue`,
   `getIntegerValue`, `getDoubleValue`, `getObjectValue` and the matching `…Details` — five concrete
   entry points per shape. The shim forwards to those and never touches a generic.
2. **A client is obtainable.** `FlagsClient.shared(named:in:)` and `.create(name:in:)` both return
   `any FlagsClientProtocol`, so there is a real object to wrap. (This is not a given — an API whose
   only instance came from a Swift-only factory would be a dead end.)

**Cost:** the shim is ~300 lines and written. What remains is the packaging — an xcframework build
step, a binding project, `ApiDefinitions.cs`, and the façade's own `IFeatureFlags` over it. Call it
a day or two, plus a device check.

**One caveat worth stating plainly:** this shim is *ours*, and Datadog is under no obligation to keep
`FlagsClientProtocol` source-stable. When they change it, the shim stops compiling — which is the
good failure, at our build time rather than someone's runtime. That risk is the price of the feature,
and it is the same risk the two convenience layers already carry.

## Profiling — **do it, and it is nearly free**

The entire public API is:

```swift
public enum Profiling {
    public struct Configuration {
        public var customEndpoint: URL?
        public var applicationLaunchSampleRate: SampleRate   // Float
        public var continuousSampleRate: SampleRate
    }
    public static func enable(with configuration: Configuration = .init(), in core: ... )
}
```

That is one `@objc` class with an `enable` and a three-property configuration object — perhaps forty
lines. There is no client, no generics, nothing to observe. If the Flags shim gets built, this rides
along in the same framework at almost no extra cost.

## OpenTelemetry — **don't**

`OTelTracerProvider.get(...)` returns `any OpenTelemetryApi.Tracer`, and every type reachable from
there — `Tracer`, `Span`, `SpanBuilder`, `AttributeValue`, `SpanContext`, the context propagators —
lives in `OpenTelemetryApi`, a pure-Swift module with no `-Swift.h` whatsoever.

Shimming this does not mean wrapping a Datadog API. It means **hand-writing an Objective-C projection
of the OpenTelemetry Swift API**, then keeping it in step with a specification we do not control, to
deliver something the .NET ecosystem already has a first-class implementation of. The shim would be
larger than everything else in the binding repo put together.

The honest recommendation is to leave iOS OpenTelemetry unreachable and say so, and point anyone who
wants OTel semantics at the OpenTelemetry .NET SDK, exporting to Datadog's OTLP intake.

---

## Suggested shape, if this goes ahead

One shim framework rather than one per feature — `DatadogNetInterop`, containing the Flags and
Profiling wrappers. Two frameworks would double the build and packaging work to save a few kilobytes
on an app that wanted only one.

```
DatadogNet.iOS/
  shims/
    DatadogNetInterop/
      Package.swift                     # or a plain xcodebuild invocation
      Sources/DatadogNetInterop/
        DatadogFlagsObjc.swift          # written, compiles
        DatadogProfilingObjc.swift
      build-xcframework.sh              # device + simulator slices, -enable-library-evolution
  src/
    DatadogNet.Interop.iOS/             # binding project over the built xcframework
      ApiDefinitions.cs
      StructsAndEnums.cs
```

Three things the build has to get right, each of which is a silent failure if missed:

1. **Both slices** — `ios-arm64` and `ios-arm64_x86_64-simulator`. Miss the simulator slice and the
   package works on device and fails to link in every developer's simulator.
2. **`-enable-library-evolution`**, so the shim itself is resilient to Swift compiler drift the way
   the Datadog frameworks are.
3. **The Datadog frameworks stay dependencies rather than being embedded.** The shim links against
   them; it must not carry a copy, or an app referencing both gets duplicate symbols.

The façade would then grow an `IFeatureFlags` with the same no-op-on-unsupported-platforms shape as
`IRumMonitor` — and, notably, **Android would be the platform with no implementation**, since
dd-sdk-android 3.12.1 has no flags module at all. That is the reverse of the usual asymmetry and
worth being explicit about in the API docs rather than discovering at runtime.
