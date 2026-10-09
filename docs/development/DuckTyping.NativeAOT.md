# DuckTyping NativeAOT Guide

## Purpose

This document describes how to use the DuckTyping NativeAOT pipeline end-to-end:

1. Author proxy definitions.
2. Generate an AOT registry assembly at build time.
3. Wire the generated registry into a NativeAOT application.
4. Validate compatibility and parity.
5. Run official DuckTyping AOT sample and parity workflows.

This guide tracks the current repository implementation and CI gate behavior.

## Documentation Map

Use this document as the entrypoint, then drill into companion docs:

1. [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md): CI/build wiring and artifact lifecycle.
2. [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md): mapping formats, generated artifact contract, and validation semantics.
3. [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md): parity summary against dynamic DuckTyping and Bible scenario families.
4. [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md): failure diagnosis and remediation playbook.
5. [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md): test commands, gates, and release-readiness checks.
6. [DuckTyping.NativeAOT.MinimalSample.md](./DuckTyping.NativeAOT.MinimalSample.md): smallest end-to-end sample app layout and commands.

## Scope and Non-Goals

This guide covers the NativeAOT DuckTyping path only. Dynamic (runtime IL emit) DuckTyping is documented in [DuckTyping.md](./DuckTyping.md) and [DuckTyping.Bible.md](./DuckTyping.Bible.md).

DuckTyping is an internal repository capability used by tracer integrations. This NativeAOT path follows that model: it provides generated registry tooling and validation for integration-owned proxy contracts, not a public customer-facing MSBuild/package integration.

Key design rule for NativeAOT:

1. No runtime proxy IL emission.
2. Proxies are generated into a separate assembly at build time.
3. Runtime only resolves and instantiates pre-registered proxies.

## Current Bible Compatibility-Gate Baseline

For the repository Bible compatibility gate (`ducktype-aot discover-mappings` + `ducktype-aot generate` + `ducktype-aot verify-compat --failure-mode strict`), all canonical map mappings are expected to behave like dynamic duck typing: `compatible`, or replaying the failure dynamic duck typing has too (`dynamicFailureReplayed: true`).

The gate contract is a single checked-in canonical map file:

`tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mappings.json`.

## Architecture Overview

NativeAOT DuckTyping has two phases.

1. Build-time phase (`ducktype-aot generate`):
   1. Resolve mappings from canonical `--map-file`.
   2. Resolve proxy and target types from provided assemblies.
   3. Expand the runtime registration set for known compatible concrete target/delegation types discovered from the provided assemblies.
   4. Emit a registry assembly containing generated proxy types and bootstrap code.
   5. Emit companion artifacts (`manifest`, `compat`, linker descriptor, props).
2. Runtime phase:
   1. Bootstrap initializes AOT mode and validates contract.
   2. Bootstrap registers forward/reverse mappings.
   3. `DuckType.GetOrCreateProxyType` and `DuckType.GetOrCreateReverseProxyType` resolve by exact key from the emitted AOT registry. The exceptions are the runtime types a registry can't name: the other array types, served by the proxy of an array mapping, and the classes of the runtime's core library built on a non-public type that the registry doesn't register (e.g. the ones only NativeAOT's core library has), served by the proxy of a mapped type of the core library they derive from or implement (see [Array targets](#array-targets) and [Non-public core library types](#non-public-core-library-types)).

### Runtime Isolation Rules

DuckType runtime mode is immutable per process.

1. First mode wins (`dynamic` or `aot`).
2. Switching mode later throws `DuckTypeRuntimeModeConflictException`.
3. AOT runtime allows a single generated registry assembly identity per process.
4. AOT runtime does not fall back to runtime IL emission, interpreted expression trees, or reflection-based compatibility scans.

### Changes to Dynamic Duck Typing

Dynamic duck typing (Reflection.Emit) is the reference behavior, and its hot path (`DuckType.Create`, `CreateCache<T>`) isn't
slower: the AOT mode check is in a method that isn't inlined into its callers, which pass results to the fast path cache by
reference. `CreateCache<T>.CreateReverse` checks per call whether the instance is a proxy (see 2). These intentional changes
compared to the previous release apply in both modes:

1. Forward and reverse proxy types are cached separately. Before, one cache keyed by the (proxy, target) pair served both, so
   `CreateReverse(T, D)` after `Create<T>(d)` (or the other way around) returned the proxy of the other direction.
2. Creating a reverse proxy over a forward proxy round-trips: `DuckType.CreateReverse`, `DuckImplement`/`TryDuckImplement` and
   `CreateCache<T>.CreateReverse` (also used by reverse duck chaining) return the original instance when the instance is a
   duck typing proxy (any `IDuckType`, e.g. a forward proxy) whose `Instance` already is an instance of the requested type.
   Before, they tried to build a reverse proxy over the generated proxy type, which normally failed. An AOT registry can't
   contain a reverse proxy for a runtime-generated proxy type, so both modes unwrap.
3. Member binding:
   - `ExplicitInterfaceTypeName` applies to properties too (before, methods only). The Protobuf integration's
     `IMessageProxy.Descriptor` declares it: its proxy can now be created for a message that only implements
     `IMessage.Descriptor` explicitly (e.g. a hand-written one), which the integration instruments (before, it couldn't, and
     logged an error on every call).
   - Comma-separated fallback names (`[Duck(Name = "A,B")]`) apply to methods too (before, properties and fields only); a
     comma inside generic arguments (`IDictionary<String,Object>.TryGetValue`) doesn't split a name. Method names are trimmed,
     even without a comma, and property and field names when they list fallback names (before, they weren't:
     `[Duck(Name = "Missing, Value")]` didn't find `Value`). Like for properties and fields, a method of the first name that has
     one wins, also when the arguments are duck chained.
   - A target method parameter the proxy method omits receives its default value, like the C# compiler passes it: the
     constant (for `nint`/`nuint` and their nullable types, converted to a native integer), `Type.Missing` for an `[Optional]`
     object, and a wrapper of null for an `[IUnknownConstant]`/`[IDispatchConstant]` object (before, no argument was loaded and
     the generated method was invalid IL). A caller information parameter (`[CallerMemberName]`...) receives its declared
     default value, and a default value of another type (a custom `CustomConstantAttribute`) fails the creation of the proxy,
     like before.
   - A proxy method parameter of type `ValueWithType<T>` passes its `Value` to the target.
   - The generic parameters of a generic proxy method have the constraints of the method it implements (before, they had
     none, and calling a target method with constraints failed verification).
   - A proxy definition method with default parameter values can be implemented (before, each parameter of the proxy method
     dynamic duck typing generated got the name, the attributes and the default value of the next one, and a default value of
     another type than its parameter's failed the creation of the proxy). The parameters of the generated method have the
     names, attributes (`in`, `out`, optional) and default values of the proxy definition's, which a duck cast of the proxy
     (a proxy of a proxy) binds, e.g. an `out` parameter (before, it failed). A default value the runtime can't store as a
     parameter constant (a `decimal`, a native integer, a `Missing` value, or null for a value type, which .NET Framework
     rejects) is left out (before, the creation of the proxy failed).
   - A reverse proxy property has the type of the contract property it implements (before, the type of the delegation's
     property: a duck cast of the reverse proxy saw that type, not the one its getter returns).
   - The emitted IL uses the right operand sizes for `ldarg.s`/`ldloc.s`/`stloc.s`/`ldloca.s`/`ldc.i4.s` and the long forms
     past 255 arguments or locals (before, negative `ldc.i4.s` constants and more than 255 locals produced invalid IL).
4. Failed results: a failed `CreateTypeResult` throws its cached exception from `CreateInstance<T>` for any `T` (before,
   `CreateInstance<object>` threw an `InvalidCastException`), and proxy definitions that can't be a generic argument (pointers,
   by-refs, by-ref-like types such as `Span<T>`, `TypedReference`, `ArgIterator`, `RuntimeArgumentHandle`, `void`) give a
   failed result instead of an exception from `GetOrCreateProxyType`. Object-based creation (`DuckType.Create(Type, object)`,
   `DuckAs`, `TryDuckCast`, `CreateReverse(Type, object)`, `DuckImplement`...) calls an object activator bound to the typed
   one instead of `DynamicInvoke` (about six times faster); exceptions are still wrapped in a `TargetInvocationException`.
5. The base constructor a class proxy calls is the parameterless instance constructor of the proxy definition (before, its
   static constructor could be selected, and every creation of the proxy threw an `InvalidProgramException`).
6. `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH` (see [Dynamic Discovery to Map Workflow](#dynamic-discovery-to-map-workflow))
   records each new (proxy, target) pair when a dynamic proxy type is created. The variable is read once per process, the
   first time a pair is recorded. When it isn't set, recording costs one check per new pair.

## NativeAOT Runtime APIs

NativeAOT bootstrapping uses these public APIs on `DuckType`:

1. `DuckType.EnableAotMode()`
2. `DuckType.ValidateAotRegistryContract(...)`
3. `DuckType.RegisterAotProxy(...)` and `DuckType.RegisterAotReverseProxy(...)` (object activators)
4. `DuckType.RegisterAotFallbackProxy(...)` (the runtime types a registry can't name: other array types, classes of the core library built on a non-public type)
5. `DuckType.RegisterAotProxyFailureFactory(...)` and `DuckType.RegisterAotReverseProxyFailureFactory(...)` (the failures of the mappings that aren't `compatible`)

The generated bootstrap calls them automatically, along with internal members of Datadog.Trace (the registry ignores the access checks of the assemblies it uses).

Current generated bootstrap behavior:

1. It registers each proxy with a typed activator: a `CreateProxyInstance<TProxy>` delegate (the delegate type of dynamic duck typing's activators, which `CreateInstance<T>` calls directly) to a generated method (`ActivateTypedProxy_XXXX`), bound to the object activator (`ActivateProxy_XXXX`, a `Func<object?, object?>`), which object-based creation (`DuckType.Create(Type, object)`, `DuckAs`...) calls. Neither depends on runtime generic binding or reflective invocation.
2. Failures are registered with direct `Func<Exception>` delegates (`CreateFailure_XXXX`) through `RegisterAotProxyFailureFactory` and `RegisterAotReverseProxyFailureFactory`. Registration creates and caches the exception without throwing. Later calls preserve the dynamic exception type and message, and the chain of inner exceptions dynamic duck typing's exception has (e.g. the `TypeLoadException` of Reflection.Emit, or an `ArgumentException` wrapping a `VerificationException`) as far as their types are public types of the core library with a constructor taking the message; they reuse the cached instance on .NET 6 and later, and clone it on older runtimes.
3. The `RuntimeMethodHandle` activator registration overloads, and the `Action`, exception type and parameterless `void` method-handle failure overloads, remain for legacy/internal callers and focused engine tests. The activator handle overloads accept object-bridge activator handles only; typed activator handles are rejected so NativeAOT does not depend on runtime generic binding or reflective invocation.
4. Each registration is in its own method, called in a `try`/`catch`: a registration that references a type the application's runtime doesn't have (e.g. a type of the generator's core library that NativeAOT's core library doesn't define, which NativeAOT compiles into a method that throws) fails alone, and the other ones are registered. The number of failed registrations, and the exception of the first one, are part of the message of the exception a lookup without registration throws.
5. The public manual registration overloads remain for compatibility but are deprecated for application code. The supported model is generated bootstrap only.

## Proxy Definition Authoring

You define proxies the same way as dynamic DuckTyping (see Bible for all semantics), then provide explicit mapping for AOT generation.

### Forward Proxy via Interface

```csharp
using Datadog.Trace.DuckTyping;

[DuckType("MyCompany.External.HttpRequest", "MyCompany.External")]
internal interface IHttpRequestProxy
{
    [Duck("Method")]
    string Method { get; }

    [Duck("GetHeader")]
    string? GetHeader(string name);
}
```

Emission behavior for interface proxies:

1. By default, generated interface proxies are value types (`struct`) that implement both the proxy interface and `IDuckType`.
2. If the proxy interface is marked with `[DuckAsClass]`, generator emits a class proxy instead.

### Forward Proxy via Class

```csharp
using Datadog.Trace.DuckTyping;

[DuckType("MyCompany.External.Payload", "MyCompany.External")]
internal abstract class PayloadProxy
{
    [DuckField(Name = "_size")]
    public abstract int Size { get; }
}
```

### DuckCopy Struct Proxy

```csharp
using Datadog.Trace.DuckTyping;

[DuckCopy("MyCompany.External.RoutePattern", "MyCompany.External")]
internal struct RoutePatternProxy
{
    public string RawText;
    public int ParameterCount;
}
```

A proxy has a `[DuckType]` (or `[DuckCopy]`) attribute per target type it is used with (e.g. the same type in several assemblies or namespaces across library versions). `[DuckCopy]` without arguments only marks the struct as a copy proxy.

### Derived Types

A proxy used with the classes deriving from a type (e.g. the response types of a library, which the generator can't list) declares the base type with `IncludeDerivedTypes = true`:

```csharp
[DuckType("MyCompany.External.ResponseBase", "MyCompany.External", IncludeDerivedTypes = true)]
internal interface IResponseProxy
{
    int StatusCode { get; }
}
```

The registry registers the proxy of the base type with `DuckType.RegisterAotDerivedTypesProxy`, and a lookup for a class without its own registration that derives from it (or implements it, for an interface) uses the registration of its most derived registered base class, else of its most derived registered interface. The proxy binds the members of that type (which the class may override), and reports the class as `IDuckType.Type`, like the proxy dynamic duck typing creates for it. Members only the derived classes have aren't bound: declare the types that add them too. Value types and sealed types don't have derived types.

### Reverse Proxy

Reverse mappings are supported and are typically declared with type-level `[DuckReverse]` attributes, then materialized into the canonical map through `ducktype-aot discover-mappings`. A reverse proxy created from a runtime type is declared on the type it delegates to, with the type it derives from (or implements):

```csharp
[DuckReverseDelegation("MyCompany.External.IEventListener", "MyCompany.External")]
internal class EventListenerDelegation
{
    [DuckReverseMethod]
    public void OnEvent(string name)
    {
    }
}
```

### Assembly-Level Mappings

The proxies of another assembly, and closed generic proxies, are declared at assembly level:

```csharp
[assembly: DuckTypeMapping("Datadog.Trace.IScope", "Datadog.Trace.Manual", "Datadog.Trace.Scope", "Datadog.Trace")]
```

```json
{
  "mode": "reverse",
  "proxyType": "MyNamespace.IReverseContract",
  "proxyAssembly": "My.Proxy.Assembly",
  "targetType": "MyNamespace.ReverseDelegation",
  "targetAssembly": "My.Target.Assembly"
}
```

### Base-Type Fallback

NativeAOT generation preserves the dynamic duck-typing `FallbackToBaseTypes` behavior for property, field, and generated accessor binding paths. It does not extend fallback to arbitrary method binding. If a proxy method needs a private method declared on a base type, add an explicit supported mapping or expose the member through a property/field-style binding.

## Mapping Sources

`ducktype-aot discover-mappings` discovers mappings from proxy assembly attributes (`[DuckType]`, `[DuckCopy]`, `[DuckReverse]`, `[DuckReverseDelegation]`, `[assembly: DuckTypeMapping]`) and writes a canonical map. `dd-trace aot instrument` reads the ones of `Datadog.Trace` (and of the other `Datadog.*` assemblies of the application) the same way, in addition to the duck typing constraints of the integrations and the recorded maps.
Discovery resolves target types from `--target-folder` inputs (plus `--target-filter`) and requires at least one `--target-folder`.

`ducktype-aot generate` consumes only `--map-file`.
When `--discover-mappings` is set, generate performs discovery first (same proxy/target inputs), adds the discovered mappings to `--map-file` (creating it if missing or empty; the mappings it already contains are kept, so remove the ones that no longer apply yourself), then continues generation in the same invocation. The map is updated under the lock a recording application takes (`<map>.lock`), atomically, and as text: its comments, formatting and encoding (UTF-8, or the one of its byte order mark) are kept. JSON the text edit doesn't handle (e.g. a constructor like `new Date(...)`) is written back from its document, without its comments, when that keeps its numbers as written (else generate fails, asking to add the mappings to the map yourself); a map the map parser rejects (e.g. content after its root object, another `schemaVersion`, bytes that aren't UTF-8 without a byte order mark), or whose `mappings` isn't a single array, is left as it is, with an error. Discovery writes its mappings to a temporary file next to the map, which is merged into the map (a missing map is created) and deleted. When discovery fails, generate warns and uses the existing map as is, or fails when there is no map.
The map is replaced by a new file, like the recording application writes it: a map the process can't write isn't written (error), and the new file is owned by the user writing it, with the permissions of the map it replaces (generate on .NET 7 and later, on Unix) or the default permissions of new files. On .NET 6 and later (the runtime of the recording application, and of the generator), a symbolic link keeps pointing to the map: the file it links to is replaced, under the lock of that file; with older runtimes, the link itself is replaced by a regular file.
Generation also registers the other runtime types a mapped target can have: the target assembly types that derive from it (or implement it), and for generic ones the closed types of `--generic-instantiations` (no other instantiation is known at build time), the underlying type of a mapped `Nullable<T>` (a boxed nullable is a boxed `T`), and the reverse proxy types the registry generates for a mapped target. Like dynamic duck typing, which creates a proxy per runtime type, each of these aliases gets its own proxy, bound to its own members, or replays its own failure. Types the registry can't reference are never aliases (the module type, `System.__Canon`, `void`, `TypedReference`, `ArgIterator`, `RuntimeArgumentHandle`, by-ref-like types). The non-public types of the core library are aliases too (a mapping to a type of the core library needs the core library among the target assemblies, see [Non-public core library types](#non-public-core-library-types)), and a forward mapping to `Task` (or another type of the core library `Task<VoidTaskResult>` derives from or implements) gets `Task<VoidTaskResult>`, the type of `Task.CompletedTask` and of the tasks of async methods, which no input names. A mapping whose target array types are assignable to (`object`, `IEnumerable`, `IList<T>`...) also gets the proxy of a representative array type (`object[]`, or `T[]` for a generic interface of `T`), which serves the arrays (see [Array targets](#array-targets)). Aliases are internal to the generated registry and do not change the canonical map file or compatibility matrix contract, except that an alias failing only in the registry makes its mapping not `compatible`, flagged `failsOnlyForOtherRuntimeTypes`.

### Array targets

Dynamic duck typing binds the members of `System.Array` for any array type, so one generated proxy per array mapping serves every array type at runtime: the proxy stores the instance as `System.Array`, and `IDuckType.Type` reports the array type it was created for (the looked-up type, like dynamic duck typing). The registry registers it with `DuckType.RegisterAotFallbackProxy`; a lookup for an array type without its own registration uses the most derived registered array type assignable from it, else a registration of the same proxy for another array type. That covers `[DuckCopy]` struct mappings too, and the arrays a mapping that isn't an array type is assignable from (`object`, `IEnumerable`, `IList<T>`...): the registry registers the proxy of a representative array type for it (`object[]`, or `T[]` for a generic interface of `T`). A failure registered for an array type is thrown for the other ones with their own name in the message, and the activator throws the `InvalidCastException` of dynamic duck typing's activator for an instance that isn't of the looked-up type.

A `System.Array` mapping serves `System.Array` lookups only: dynamic duck typing binds other members on `System.Array` (e.g. its explicit interface implementations) than on an array type.

The methods the runtime adds to each array type (`Get`, `Set`, `Address`) aren't in metadata: a proxy method dynamic duck typing binds to one of them makes the mapping not `compatible` (`DTAOT0207`, the detail names the runtime method).

### Non-public core library types

The non-public types of the runtime's core library (e.g. `System.RuntimeType`, the enumerators of its collections, its `Stream` wrappers, or the box types of async methods, which is what a `Task<T>` often is) differ between runtimes: NativeAOT's core library doesn't define all of CoreCLR's, and has its own. Discovery records the runtime type of the instance, so a recorded map can name them, and the mapping of its closest public base class.

1. The generator registers the ones it knows like the other runtime types of a mapped target (see [Mapping Sources](#mapping-sources)): each gets its own proxy, bound to its own members, or replays its own failure, like dynamic duck typing. That needs the core library among the target assemblies: a `--target-folder` with the application runtime's `System.Private.CoreLib.dll` (e.g. the runtime's shared framework directory, or the publish directory of a self-contained application). Each registration is isolated: one the application's runtime can't make (NativeAOT's core library doesn't define the type, or a member it uses) fails alone.
2. A class of the core library built on a non-public type (its definition or a generic argument is one, e.g. `Task<VoidTaskResult>` and the boxes of async methods deriving from it) that the registry doesn't register is served by a registration of a type of the core library it derives from or implements, made with `DuckType.RegisterAotFallbackProxy` for the mappings of the core library's interfaces and classes that aren't sealed: the one of its most derived registered base class (other than `object`), else of the most derived registered interface it implements, else of `object`. The proxy binds the members of that type (which the runtime type may override, or hide), and reports the looked-up type as `IDuckType.Type`, like the proxy dynamic duck typing creates for it. A failure of that registration isn't replayed (it's the one of another type): the lookup throws the missing registration exception. Value types aren't served this way (a proxy copies a value-type instance).
3. A mapping that targets, or whose proxy binds, a type or member of the core library that isn't public is specific to the generator's runtime: generate warns (`DTAOT0216`), and the compatibility matrix flags it `runtimeSpecific`. Map a public type of the core library, with public members, for an application that runs on another runtime (e.g. NativeAOT).

### How the Generator Matches Dynamic Duck Typing

The generator runs the dynamic duck typing engine of its own Datadog.Trace on the application's types, and binds what it binds: the same target members, the same duck chaining decisions, the same reverse implementations (methods and properties). It creates the proxy types like dynamic duck typing does (once per proxy type shape: a forward proxy type takes its members from the proxy, and from the target only the attributes of its ToString and its `[DuckInclude]` methods): a mapping dynamic duck typing can't create fails the same way in the registry, even when the registry could have generated a proxy for it (events, members Reflection.Emit can't implement...).

Like dynamic duck typing, a forward proxy implements the accessors of the properties `Type.GetProperties()` returns for the proxy type (for a class, the virtual ones: a property is hidden by an override, or by a more derived property with the same name and signature, not one of another signature or a private one of a base type), then those of its interfaces whose name isn't taken yet, and no event accessors: the others keep the implementation of the proxy type.

A class proxy also implements the accessors of the properties of its interfaces whose name isn't taken (`AddInterfaceProperties`, in `Type.GetInterfaces()` order): dynamic duck typing defines a public virtual method for each one, which overrides a base virtual method with the same name and signature (even a protected one) and fails like dynamic duck typing when that method is `final`. The proxy's `ToString` calls the target's public `ToString()` the way `Type.GetMethod("ToString", Type.EmptyTypes)` selects it: through the `object.ToString` slot when the selected method overrides it, and directly otherwise (a `new virtual` method, or an override of one). A static `ToString`, or a non-virtual generic one, isn't called; a virtual generic one gives the proxy a `ToString` that throws the runtime's `BadImageFormatException`, like the one dynamic duck typing creates.

A reverse proxy instance gets forward proxies too: dynamic duck typing creates them for its runtime type, the reverse proxy type the registry generates, so the registry registers the forward mappings of the proxy types mapped to its ancestors for it, once per (proxy type, reverse proxy type). Each is bound to the members of the generated reverse proxy type, selected by dynamic duck typing over the reverse proxy type it creates for the same pair (its own `IDuckType` members, `ToString`, members hiding those of the contract, its private `_currentInstance` field, the overrides of protected contract members, the `[DuckInclude]` methods it doesn't override as `final`...). A failure of dynamic duck typing is replayed (e.g. setting a member of a struct reverse proxy), and a proxy the registry can't create where dynamic duck typing does makes every forward mapping of the group not `compatible`. When the generator can't evaluate the pair with dynamic duck typing (a metadata-only mapping), the registration is bound from metadata and flagged `checkedAgainstMetadataOnly`.

A forward proxy instance gets forward proxies too (a proxy of a proxy, e.g. `DuckType.Create(typeof(IFoo), fooProxy)`): the registry registers, for each forward proxy type it generates, the forward mappings whose target is its proxy definition type (or a base type of it other than `object`, `ValueType` and `IDuckType`), like for the reverse proxy types it generates. Discovery records such a pair with the proxy definition type as the target (the proxy type of dynamic duck typing can't be named). The generated proxy methods have the parameters (names, `in`/`out`/optional, default values) of the methods they implement, like dynamic duck typing's, which the proxy of a proxy binds.

The registry calls a target member the way the proxy dynamic duck typing creates does: with `callvirt` for a public or generic method of a reference type (virtual dispatch, and a `NullReferenceException` for a null instance), and the exact bound method otherwise (no virtual dispatch: a proxy created for a base type calls the base type's member on an instance of a derived type, and no null check). A virtual call to a public override declared by a non-public type of the core library goes through the public method it overrides, which every runtime's core library has. The arguments and return values of the members dynamic duck typing selects get its conversions: boxing a value type to `object`, `ValueType` or an interface it implements, unboxing, an enum to or from its underlying type, `Nullable<T>`, duck chaining..., for parameters, by-ref parameters, setters and indexers, forward and reverse.

`[DuckInclude]` methods whose signature uses a generic parameter of their declaring type are bound with the type's generic arguments. A proxy type that is an array type (e.g. a mapping recorded for an array member, `IProxy[]`) can't be created by dynamic duck typing: the registry replays its failure.

A mapping bound from metadata (`checkedAgainstMetadataOnly`) approximates dynamic duck typing: target methods are selected like `Type.GetMethod` with the proxy's parameter types (its default binder: an exact match, else the most specific method whose parameters accept the proxy's), then like dynamic duck typing's candidate scan, and the failures it predicts have the exception type and message of dynamic duck typing's for the common cases (a missing member, an invalid conversion, a proxy type Reflection.Emit can't create, with its inner `TypeLoadException`), which can still differ.

That requires loading the application's assemblies in the generator process:

1. Run the generator on a runtime that can load them (the application's runtime or a newer one). For a mapping whose types it can't load, generate warns and binds from metadata, which approximates dynamic duck typing.
2. Use the generator of the application's Datadog.Trace build: proxies declared with another build, or contracts that declare their own duck typing attributes, are bound from metadata too.
3. Generate loads the registry it wrote, when it can, without running any of its code: a generated proxy type the runtime can't load would make the whole registry fail to load (dynamic duck typing can't create that proxy either), so the registry is emitted again with a failure registration for it, at most three emissions in total (the answers of dynamic duck typing are kept between them). When it can't load the registry (the generator can't load what the proxy types use), it warns.
4. The registry calls the Datadog.Trace API of the generator: generate fails when the application's Datadog.Trace (another build) doesn't define a type or member the registry uses.

The compatibility matrix marks the mappings bound from metadata with `checkedAgainstMetadataOnly: true`.

The registry only references the application's Datadog.Trace (the one passed as a target or proxy assembly), even for the members of Datadog.Trace it imports from the generator's own copy.

A registry generated by a previous run is never used as a target assembly, even when `--output` is inside a `--target-folder`: generate skips it with a warning.

## Map File Schema

`--map-file` accepts JSON with `schemaVersion` and `mappings`.

```json
{
  "schemaVersion": "1",
  "mappings": [
    {
      "mode": "forward",
      "proxyType": "My.Namespace.IProxy",
      "proxyAssembly": "My.Proxy.Assembly",
      "targetType": "My.Namespace.TargetType",
      "targetAssembly": "My.Target.Assembly"
    },
    {
      "mode": "reverse",
      "proxyType": "My.Namespace.IReverseProxy",
      "proxyAssembly": "My.Proxy.Assembly",
      "targetType": "My.Namespace.ReverseDelegation",
      "targetAssembly": "My.Target.Assembly"
    }
  ]
}
```

Notes:

1. `mode` defaults to `forward`.
2. Valid modes: `forward`, `reverse`.
3. `proxyType` and `targetType` can be assembly-qualified; if so, assembly can be inferred.
4. Open generic map rules are allowed only when they can be expanded from matching closed `--generic-instantiations` roots before registry emission: an open proxy and an open target with the same arity (each closed root of the target closes both), a non-generic proxy and an open target (e.g. a proxy of every `Message<TKey, TValue>`: each closed root of the target gets the proxy), or an open proxy and a non-generic target (each closed root of the proxy gets the target).
5. The proxy and target assemblies of a mapping are resolved from the `--proxy-assembly` inputs and the target inputs alike: a recorded map names, e.g., the contract assembly of a library a reverse proxy implements.

## Generic Instantiation Roots

Use `--generic-instantiations` to preserve additional closed generic roots.

`verify-compat` expands the same open map rules using the closed roots recorded in `--manifest`. When verifying without a manifest, pass the same `--generic-instantiations` file used for generation. Every expanded closed mapping must be present in the compatibility matrix.

Supported JSON forms:

```json
[
  "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]], System.Private.CoreLib",
  {
    "type": "My.Namespace.GenericBox`1[[My.Namespace.UserType, My.Assembly]]",
    "assembly": "My.Assembly"
  }
]
```

Rules:

1. Entries must be closed generic types.
2. Open generic forms are rejected.
3. Closed roots may expand matching open generic map rules into closed registry mappings.

## Running the Generator

The command is currently hidden from root help, but callable directly.

Runner assembly path (Release build):

`artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll`

### Minimal Command

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-folder /abs/path/targets \
  --target-filter "*.dll" \
  --map-file /abs/path/ducktype-aot-map.json \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.dll
```

### One-step Command (discover + generate)

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --discover-mappings \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-folder /abs/path/targets \
  --target-filter "*.dll" \
  --map-file /abs/path/ducktype-aot-map.json \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.dll
```

### Full Command (recommended)

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-folder /abs/path/targets \
  --target-folder /abs/path/extra-targets \
  --target-filter "*.dll" \
  --map-file /abs/path/ducktype-aot-map.json \
  --generic-instantiations /abs/path/ducktype-aot-generic-instantiations.json \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.MyService \
  --emit-trimmer-descriptor /abs/path/Datadog.Trace.DuckType.AotRegistry.MyService.linker.xml \
  --emit-props /abs/path/Datadog.Trace.DuckType.AotRegistry.MyService.props \
  --strong-name-key-file /abs/path/mykey.snk \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.MyService.dll
```

Strong-name key can also be provided with:

`DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE=/abs/path/mykey.snk`

### Generator Validation Rules

Generator fails if:

1. No `--proxy-assembly` is provided.
2. No `--target-folder` is provided.
3. Required file/directory paths do not exist (except `--map-file` when `--discover-mappings` is set).
4. Open generic map rules cannot be expanded to closed mappings.
5. No compatible mappings are resolved from the canonical map file.
6. The application's Datadog.Trace doesn't define a type or member of Datadog.Trace the registry uses (an older version than the generator's).

## Generated Artifacts

Given output `X.dll`, generator emits:

1. `X.dll`:
   1. generated proxy types. Like dynamic duck typing's, their only public constructor takes the target instance: code that creates proxies with `ProxyType.GetConstructors()[0]` (CallTarget's IntegrationMapper, the Activity listeners) works with them. A proxy that also serves other runtime types (array types, non-public core library types) has an internal constructor receiving the type it reports as `IDuckType.Type`, which IntegrationMapper calls with the target type.
   2. bootstrap type `Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap`.
   3. module initializer that calls bootstrap `Initialize()` when dynamic code isn't supported (NativeAOT), see [Option B](#option-b-direct-references). The registration runs once: an explicit `Initialize()` call after it does nothing.
   4. the methods registered: proxy activators (`CreateProxy_XXXX(<targetType>)`) with their object activators (`ActivateProxy_XXXX(object)`) and typed activators (`ActivateTypedProxy_XXXX`), fallback activators (`ActivateFallbackProxy_XXXX`), and failure factories (`CreateFailure_XXXX`).
2. `X.dll.manifest.json`: build metadata, assembly fingerprints, mapping snapshot.
3. `X.dll.compat.json`: machine-readable compatibility matrix per mapping.
4. `X.dll.compat.md`: human-readable compatibility report (statuses marked `(replayed)` replay a dynamic duck typing failure, `(metadata only)` were bound from metadata, `(other runtime types)` fail only for other runtime types of their target, and `(runtime specific)` use a type or member of the core library that isn't public).
5. Linker descriptor:
   1. default `X.dll.linker.xml`, or `--emit-trimmer-descriptor` path.
   2. roots the bootstrap type of the registry, and the proxy type definitions of compatible mappings (and of the mappings flagged `failsOnlyForOtherRuntimeTypes`, whose proxy is registered), with `/` between nested types and the other characters reflection escapes unescaped. Target types and generated proxy types aren't rooted: the bootstrap references them (and the target members they use), and a type the trimmer or NativeAOT's compiler can't load (e.g. a type of the generator's core library that NativeAOT's doesn't define) would fail the whole build, where its registration only fails alone at runtime. Closed generics and arrays aren't rooted by name (descriptors can't name them, IL2008): the registry code keeps what it uses.
6. Props file:
   1. default `X.dll.props`, or `--emit-props` path.
   2. adds registry reference + `TrimmerRootDescriptor`.

Allocation/IL notes for generated activators:

1. Typed activators avoid constructor-time object casts (constructor receives typed target).
2. Bridge activators may still contain `castclass`/`unbox.any` because registration entrypoint receives `object`.
3. Boxing remains in parity-required edges such as:
   1. returning a value-type generated proxy through an interface/object contract.
   2. `IDuckType.Instance` when wrapped target is a value type.

Note:

1. In the managed build Bible compatibility gate, the generated `Datadog.Trace.DuckType.AotRegistry.BibleGate.dll` is written to a clean build-data directory and treated as a transient verification artifact.
2. Compatibility artifacts are retained in that gate output directory (`*.manifest.json`, `*.compat.json`, `*.compat.md`, props, linker descriptor, generated map) for diagnostics and drift review, not for application reuse.

## Application Wiring

### Option A (recommended): Import generated props

In your app `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <Import Project="$(DuckTypeAotPropsPath)"
          Condition="'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')" />
</Project>
```

Publish/build with:

```bash
/p:DuckTypeAotPropsPath=/abs/path/Datadog.Trace.DuckType.AotRegistry.MyService.props
```

### Option B: Direct references

1. Reference generated registry assembly directly.
2. Add linker descriptor manually.

The registry's module initializer initializes it when dynamic code isn't supported (NativeAOT): a NativeAOT application doesn't call anything. Under the JIT (e.g. `dotnet run`, or with the Datadog automatic instrumentation, whose tracer uses dynamic duck typing), the module initializer runs too, as soon as a method referencing the registry is compiled, but leaves the process in dynamic duck typing. To use the registry under the JIT (e.g. to test it, see [Run as Regular .NET App](#7-run-as-regular-net-app-with-aot-registry)), call `Initialize()` before any duck typing: it switches the process to AOT duck typing, or throws `DuckTypeRuntimeModeConflictException` when dynamic duck typing already ran. Under NativeAOT, calling it does nothing more.

```csharp
using Datadog.Trace.DuckTyping.Generated;

DuckTypeAotRegistryBootstrap.Initialize();
```

## Hands-On Quickstart: Create and Run an AOT DuckTyping App

This section is a full from-scratch example you can copy/paste.

### Prerequisites

1. Repository root is available as `REPO_ROOT`.
2. The SDK required by the repository's `global.json` is installed, along with the .NET 8 runtime for the managed sample.
3. NativeAOT toolchain prerequisites are installed for your OS (clang/Xcode build tools on macOS/Linux, C++ toolchain on Windows).

### 1. Build Datadog.Trace and Runner

```bash
cd "$REPO_ROOT"
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj -c Release -f net6.0
dotnet build tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj -c Release -f net8.0
```

### 2. Create Sample Solution

```bash
WORK_DIR=/tmp/ducktype-aot-quickstart
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR"
cd "$WORK_DIR"

dotnet new classlib -n SampleDuckContracts --no-restore
dotnet new console -n SampleDuckApp --no-restore
```

### 3. Add Contracts/Targets/Proxies

Create `SampleDuckContracts/ValueContracts.cs`:

```csharp
using Datadog.Trace.DuckTyping;

namespace SampleDuckContracts;

public interface IValueProxy
{
    int GetValue();
}

public interface IReverseValueProxy
{
    int DoubleValue(int value);
}

public struct ValueCopyProxy
{
    public int Value;
}

public sealed class ValueTarget
{
    private readonly int _value;

    public ValueTarget(int value)
    {
        _value = value;
    }

    public int GetValue() => _value;
}

public sealed class ReverseValueDelegation
{
    [DuckReverseMethod]
    public int DoubleValue(int value) => value * 2;
}

public sealed class ValueCopyTarget
{
    public ValueCopyTarget(int value)
    {
        Value = value;
    }

    public int Value { get; set; }
}
```

Reverse implementations require `[DuckReverseMethod]`, matching dynamic duck typing. The tracer's attributes are internal. For this standalone AOT sample, create `SampleDuckContracts/DuckReverseMethodAttribute.cs` with metadata that the generator recognizes:

```csharp
namespace Datadog.Trace.DuckTyping;

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class DuckReverseMethodAttribute : System.Attribute
{
    public string? Name { get; set; }
}
```

Set `SampleDuckContracts/SampleDuckContracts.csproj` to target .NET 8 explicitly. New SDK templates only offer their current framework; the app project below also sets its framework explicitly.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

Build contracts:

```bash
dotnet build SampleDuckContracts/SampleDuckContracts.csproj -c Release
```

### 4. Create Mapping File

Create `ducktype-aot-map.json`:

```json
{
  "mappings": [
    {
      "mode": "forward",
      "proxyType": "SampleDuckContracts.IValueProxy",
      "proxyAssembly": "SampleDuckContracts",
      "targetType": "SampleDuckContracts.ValueTarget",
      "targetAssembly": "SampleDuckContracts"
    },
    {
      "mode": "reverse",
      "proxyType": "SampleDuckContracts.IReverseValueProxy",
      "proxyAssembly": "SampleDuckContracts",
      "targetType": "SampleDuckContracts.ReverseValueDelegation",
      "targetAssembly": "SampleDuckContracts"
    },
    {
      "mode": "forward",
      "proxyType": "SampleDuckContracts.ValueCopyProxy",
      "proxyAssembly": "SampleDuckContracts",
      "targetType": "SampleDuckContracts.ValueCopyTarget",
      "targetAssembly": "SampleDuckContracts"
    }
  ]
}
```

### 5. Generate AOT Registry + Artifacts

```bash
CONTRACTS_DLL="$WORK_DIR/SampleDuckContracts/bin/Release/net8.0/SampleDuckContracts.dll"
RUNNER_DLL="$REPO_ROOT/artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll"
REGISTRY_DLL="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.dll"
REGISTRY_PROPS="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.props"
REGISTRY_LINKER="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.linker.xml"

dotnet "$RUNNER_DLL" ducktype-aot generate \
  --proxy-assembly "$CONTRACTS_DLL" \
  --target-folder "$(dirname "$CONTRACTS_DLL")" \
  --target-folder "$REPO_ROOT/artifacts/bin/Datadog.Trace/release_net6.0" \
  --target-filter "*.dll" \
  --map-file "$WORK_DIR/ducktype-aot-map.json" \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.Sample \
  --emit-props "$REGISTRY_PROPS" \
  --emit-trimmer-descriptor "$REGISTRY_LINKER" \
  --output "$REGISTRY_DLL"
```

This sample uses the explicit map file because its proxy types have no type-level discovery attributes. `--discover-mappings` requires that metadata; it cannot infer these mappings from the sample's member shapes.

You should now have:

1. `Datadog.Trace.DuckType.AotRegistry.Sample.dll`
2. `Datadog.Trace.DuckType.AotRegistry.Sample.dll.manifest.json`
3. `Datadog.Trace.DuckType.AotRegistry.Sample.dll.compat.json`
4. `Datadog.Trace.DuckType.AotRegistry.Sample.dll.compat.md`
5. `Datadog.Trace.DuckType.AotRegistry.Sample.props`
6. `Datadog.Trace.DuckType.AotRegistry.Sample.linker.xml`

### 6. Wire Sample App Project

Create `SampleDuckApp/SampleDuckApp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Datadog.Trace">
      <HintPath>__DATADOG_TRACE_DLL__</HintPath>
      <Private>true</Private>
    </Reference>
    <ProjectReference Include="../SampleDuckContracts/SampleDuckContracts.csproj" />
  </ItemGroup>

  <Import Project="$(DuckTypeAotPropsPath)"
          Condition="'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')" />
</Project>
```

Replace `__DATADOG_TRACE_DLL__` with:

`$REPO_ROOT/artifacts/bin/Datadog.Trace/release_net6.0/Datadog.Trace.dll`

Generation and the app must use this same DLL: the registry validates the tracer's version and module ID at startup.

Create `SampleDuckApp/Program.cs`:

```csharp
using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.DuckTyping.Generated;
using SampleDuckContracts;

var dynamicAssemblyLoads = 0;
AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
{
    if (args.LoadedAssembly.IsDynamic)
    {
        dynamicAssemblyLoads++;
    }
};

DuckTypeAotRegistryBootstrap.Initialize();

var forwardResult = DuckType.GetOrCreateProxyType(typeof(IValueProxy), typeof(ValueTarget));
var reverseResult = DuckType.GetOrCreateReverseProxyType(typeof(IReverseValueProxy), typeof(ReverseValueDelegation));
var copyResult = DuckType.GetOrCreateProxyType(typeof(ValueCopyProxy), typeof(ValueCopyTarget));

if (!forwardResult.CanCreate() || !reverseResult.CanCreate() || !copyResult.CanCreate())
{
    Console.WriteLine("CAN_CREATE:False");
    Environment.ExitCode = 1;
    return;
}

var forwardProxy = forwardResult.CreateInstance<IValueProxy>(new ValueTarget(42));
var reverseProxy = (IReverseValueProxy)DuckType.CreateReverse(typeof(IReverseValueProxy), new ReverseValueDelegation());
var copyProxy = copyResult.CreateInstance<ValueCopyProxy>(new ValueCopyTarget(42));

Console.WriteLine("CAN_CREATE:True");
Console.WriteLine($"VALUE:{forwardProxy.GetValue()}");
Console.WriteLine($"REVERSE_VALUE:{reverseProxy.DoubleValue(21)}");
Console.WriteLine($"COPY_VALUE:{copyProxy.Value}");
Console.WriteLine($"DYNAMIC_CODE:{RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"DYNAMIC_ASSEMBLIES:{dynamicAssemblyLoads}");
```

### 7. Run as Regular .NET App (with AOT Registry)

```bash
dotnet run --project "$WORK_DIR/SampleDuckApp/SampleDuckApp.csproj" -c Release \
  /p:DuckTypeAotPropsPath="$REGISTRY_PROPS"
```

Expected output includes:

1. `CAN_CREATE:True`
2. `VALUE:42`
3. `REVERSE_VALUE:42`
4. `COPY_VALUE:42`

### 8. Publish and Run NativeAOT

Pick RID for your machine:

1. Linux x64: `linux-x64`
2. Linux arm64: `linux-arm64`
3. macOS arm64: `osx-arm64`
4. macOS x64: `osx-x64`
5. Windows x64: `win-x64`
6. Windows arm64: `win-arm64`

```bash
RID=linux-x64
PUBLISH_DIR="$WORK_DIR/publish"

dotnet publish "$WORK_DIR/SampleDuckApp/SampleDuckApp.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  /p:PublishAot=true \
  /p:InvariantGlobalization=true \
  /p:DuckTypeAotPropsPath="$REGISTRY_PROPS" \
  -o "$PUBLISH_DIR"
```

Run published binary:

```bash
"$PUBLISH_DIR/SampleDuckApp"
```

On Windows:

```powershell
& "$PUBLISH_DIR\\SampleDuckApp.exe"
```

Expected output also includes:

1. `DYNAMIC_CODE:False`
2. `DYNAMIC_ASSEMBLIES:0`

## NativeAOT Publish Sample (Manual)

This sample mirrors the official integration test flow.

### 1. Generate registry

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/SampleDuckContracts.dll \
  --target-folder /abs/path \
  --target-filter "*.dll" \
  --map-file /abs/path/ducktype-aot-nativeaot-map.json \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.NativeAotSample \
  --emit-trimmer-descriptor /abs/path/Datadog.Trace.DuckType.AotRegistry.NativeAotSample.linker.xml \
  --emit-props /abs/path/Datadog.Trace.DuckType.AotRegistry.NativeAotSample.props \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.NativeAotSample.dll
```

### 2. Publish NativeAOT app

```bash
dotnet publish /abs/path/SampleDuckNativeAotApp.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  /p:PublishAot=true \
  /p:InvariantGlobalization=true \
  /p:DuckTypeAotPropsPath=/abs/path/Datadog.Trace.DuckType.AotRegistry.NativeAotSample.props \
  -o /abs/path/publish
```

### 3. Run app and verify

Expected signals:

1. DuckType `CanCreate` checks are `True`.
2. Forward/reverse/DuckCopy values are correct.
3. `RuntimeFeature.IsDynamicCodeSupported` is `False`.
4. No dynamic assembly load activity for proxy generation.

## Official "Run Everything" Commands

### Full DuckTyping suite in Dynamic mode

```bash
DD_DUCKTYPE_TEST_MODE=dynamic \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0
```

### Full isolated Dynamic vs AOT parity orchestration

This command executes:

1. Dynamic test run with discovery output.
2. Registry generation.
3. AOT test run with generated registry.
4. Hard gate: both runs must be green and parity-equal.

```bash
DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 \
DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_SEED=20260301 \
  dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests
```

Optional artifact retention for debugging:

```bash
DD_DUCKTYPE_AOT_FULL_SUITE_PARITY_KEEP_ARTIFACTS=1
```

Use the full-suite parity orchestration command as the supported full-suite AOT gate.
Do not run full-suite AOT tests with the Bible compatibility-gate registry artifact.

### AOT processor + NativeAOT integration test suite

The NativeAOT publish test is skipped unless `DD_RUN_DUCKTYPE_AOT_NATIVEAOT_PUBLISH=1` is set:

```bash
DD_RUN_DUCKTYPE_AOT_NATIVEAOT_PUBLISH=1 \
dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAot
```

## Dynamic Discovery to Map Workflow

Use discovery when migrating existing dynamic call sites.

### 1. Run dynamic workload with discovery output

```bash
DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH=/abs/path/discovered-ducktype-aot-map.json \
DD_DUCKTYPE_TEST_MODE=dynamic \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0
```

### 2. Feed discovered map into generator

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-folder /abs/path \
  --target-filter "*.dll" \
  --map-file /abs/path/discovered-ducktype-aot-map.json \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.dll
```

Shortcut: add `--discover-mappings` to `ducktype-aot generate` to also include the mappings declared with attributes: they are added to the recorded `--map-file`, whose mappings are kept.

Note: discovery doesn't record a mapping whose proxy or target type is runtime generated (a registry can't reference it): for a forward proxy of a reverse proxy dynamic duck typing generated, it records the mapping of the type the reverse proxy was created for, which the registry serves the generated reverse proxy type with. For a class of the core library built on a non-public type, it also records the mapping of its closest public base class, whose proxy serves the runtime types another runtime has instead (see [Non-public core library types](#non-public-core-library-types)).

## Compatibility Verification Command

Use `verify-compat` as a contract gate in CI/release.

```bash
dotnet artifacts/bin/Datadog.Trace.Tools.Runner.Tool/release_net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot verify-compat \
  --compat-report /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.md \
  --compat-matrix /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.json \
  --map-file tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mappings.json \
  --mapping-catalog tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mapping-catalog.json \
  --scenario-inventory tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-scenario-inventory.json \
  --manifest /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.manifest.json \
  --failure-mode strict
```

### Verify-compat Inputs

1. `--compat-matrix` and `--map-file` are required.
2. Optional contract:
   1. `--compat-report` (checked to exist)
   2. `--manifest`
   3. `--mapping-catalog`
   4. `--scenario-inventory`
   5. `--generic-instantiations` (when no manifest supplies the closed roots)
3. `--failure-mode` values:
   1. `default`: manifest fingerprint drift warns.
   2. `strict`: manifest fingerprint drift fails.
4. Every `--map-file` mapping must be `compatible`, or replay the failure dynamic duck typing has for it (`dynamicFailureReplayed: true` in the compatibility matrix): such a mapping throws the same exception type, with the same message, in both modes, and the same inner exceptions as far as their types are public types of the core library with a constructor taking the message (see [NativeAOT Runtime APIs](#nativeaot-runtime-apis)).

For protected-branch validation, use the Nuke gate bundle:

```bash
./tracer/build.sh RunDuckTypeAotGates
```

That bundle runs strict compatibility verification, full-suite dynamic-vs-AOT parity, and NativeAOT publish validation.

`RunManagedUnitTests` depends on this bundle. In CI it only runs in one Linux x64 glibc job, which has the NativeAOT toolchain: the unit test matrix declares it (`--duck-type-aot-gates`, net9.0 or the newest framework). Locally it only runs when a DuckType AOT gate target is invoked explicitly; pass `--duck-type-aot-gates true` (or `false`) to force (or skip) the gates.

## Compatibility Status and Diagnostics

Status values the generator emits:

1. `compatible`
2. `pending_proxy_emission` (a mapping without an emission result)
3. `unsupported_proxy_kind`
4. `missing_proxy_type`
5. `missing_target_type`
6. `missing_target_method`
7. `incompatible_method_signature`

The mapping catalog's `expectedStatus` also accepts `non_public_target_method`, `unsupported_proxy_constructor` and `unsupported_closed_generic_mapping`, which the generator doesn't emit.

Current diagnostic codes emitted by generator:

1. `DTAOT0202` `unsupported_proxy_kind`
2. `DTAOT0204` `missing_proxy_type`
3. `DTAOT0205` `missing_target_type`
4. `DTAOT0207` `missing_target_method`
5. `DTAOT0209` `incompatible_method_signature`
6. `DTAOT0212` `missing_target_method` for proxy setters targeting read-only properties
7. `DTAOT0214` `incompatible_method_signature` for reverse custom attribute named arguments
8. `DTAOT0215` `unsupported_proxy_kind` for proxy types Reflection.Emit can't create (a sealed base type, an abstract member left without implementation, an event...): the registry replays the `DuckTypeException` dynamic duck typing throws. Also for generated proxy types the runtime can't load.
9. `DTAOT0216` (a warning, the status is unchanged) for a mapping that targets, or whose proxy binds, a type or member of the core library that isn't public, flagged `runtimeSpecific` (see [Non-public core library types](#non-public-core-library-types)).

A mapping that isn't `compatible` is registered as a failure. When dynamic duck typing in the generator fails to create the proxy too, the registry throws its exception (type and message) and the compatibility matrix marks the mapping with `dynamicFailureReplayed: true`. A failure of the generator process (an assembly it can't load) isn't a dynamic duck typing failure: it isn't marked, and generate warns. Other failures (`dynamicFailureReplayed: false`) behave differently in the two modes, and verify-compat rejects them.

The compatibility matrix also flags a mapping with `failsOnlyForOtherRuntimeTypes: true` when only its aliases (the other runtime types of its target, see [Mapping Sources](#mapping-sources)) fail in the registry, with `checkedAgainstMetadataOnly: true` when it (or one of its aliases) was bound from metadata instead of with dynamic duck typing in the generator, and with `runtimeSpecific: true` (`DTAOT0216`).

Known limitations (reported as not `compatible`):

1. The methods the runtime only defines on array types (`Get`, `Set`, `Address`) can't be bound: a mapping whose proxy binds one of them in dynamic duck typing is reported as not compatible (`DTAOT0207`, naming the runtime method). The other array types a mapped array can have at runtime are served by the proxy of the array mapping (see [Array targets](#array-targets)).
2. A mapping whose aliases (derived types, generic instantiations, generated reverse proxy types, the representative array type, non-public core library types) fail only in the registry is reported as not compatible and flagged `failsOnlyForOtherRuntimeTypes`: the mapping behaves like dynamic duck typing for its target type, and the registry registers its proxy, but not for those runtime types. Discovery keeps such mappings in the map it writes (with a warning).

Known differences the compatibility matrix doesn't report:

1. Under NativeAOT, some exception messages are the runtime's own: the message of an exception created from an HRESULT, and the names of nested generic types in the messages of the failures of the runtime types a registration serves (see [Array targets](#array-targets)).
2. When a `[DuckInclude]` method of the target has the name and signature of a proxy method, dynamic duck typing declares two methods with that signature on the proxy type (reflection then finds them ambiguous), the registry one: two such methods are invalid metadata (ECMA-335 II.22.26), which the NativeAOT compiler may reject.
3. A registration whose types the application's runtime can't load fails at startup (see [NativeAOT Runtime APIs](#nativeaot-runtime-apis)).

Bible catalog `expectedStatus` overrides:

1. none (all required mappings default to `compatible`, which a mapping that replays its dynamic failure also meets: the Bible covers failure scenarios too).

## Environment Variables Summary

### Generation and Build

1. `DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE`
   1. Optional strong-name key path for generated registry signing.
2. `DD_TRACE_DUCKTYPE_AOT_PROFILE`
   1. `1` or `true` makes generate print the time of its phases (`ducktype-aot profile:` lines), to diagnose slow generations.

### Discovery and Test Harness

1. `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH`
   1. Test/migration workflow output path for dynamic discovery map entries.
   2. Every process that inherits the variable merges its mappings into the same file (coordinated through a `.lock` file next to it, or next to the file it links to when it's a symbolic link), so delete the file before a fresh discovery run.
   3. For migrating to NativeAOT and for testing: don't set it in production.
2. `DD_DUCKTYPE_TEST_MODE`
   1. `dynamic` or `aot` for test runtime bootstrap.
3. `DD_DUCKTYPE_AOT_REGISTRY_PATH`
   1. Test runtime path to generated registry assembly.
4. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY`
   1. Enables full suite parity integration orchestration test.
5. `DD_RUN_DUCKTYPE_AOT_NATIVEAOT_PUBLISH`
   1. Enables the NativeAOT publish integration test (set by `RunDuckTypeAotNativeAotPublishGate`).

The Nuke build parameter `--duck-type-aot-gates true|false` forces the gate bundle on or off for `RunManagedUnitTests`.

## Troubleshooting

### `AOT duck typing mapping not found`

Cause:

1. Requested proxy-target pair is not registered in AOT registry.

Actions:

1. Confirm mapping exists in map/attribute discovery and output `.compat.json`.
2. Confirm bootstrap `Initialize()` runs before first DuckType call (under the JIT, only an explicit call initializes the registry).
3. Confirm correct registry assembly is loaded.
4. When the message says registrations of the registry failed at startup, the missing one may be one of them: a type it uses can't be loaded by the application's runtime (e.g. a non-public type of the core library, see [Non-public core library types](#non-public-core-library-types), or an assembly the application doesn't ship).

### `DuckType runtime mode is immutable after initialization`

Cause:

1. Process initialized in one mode, then switched.

Actions:

1. Keep dynamic and AOT runs process-isolated.
2. In tests, start process with explicit mode env var.

### `single generated registry assembly per process`

Cause:

1. Multiple different generated registries attempted to register.

Actions:

1. Load exactly one registry assembly per process.
2. Avoid mixing registries from different builds/services.

### Contract validation failures

Cause:

1. Registry was generated against different Datadog.Trace assembly version/MVID/schema.
2. The application's Datadog.Trace.dll wasn't a generate input (through `--target-folder` or `--proxy-assembly`): the registry is then bound to the generator's own copy, and generate warns about it.
3. The application is published with `PublishTrimmed=true` without NativeAOT: ILLink rewrites Datadog.Trace.dll with another MVID, which no generated registry can match. This configuration isn't supported.

Actions:

1. Regenerate registry with the same Datadog.Trace build used at runtime, passing that Datadog.Trace.dll to generate.
2. Re-check manifest metadata (`datadogTraceAssembly`).

### Open generic rule did not expand

Cause:

1. Map file contains an open generic proxy/target rule.
2. No matching closed `--generic-instantiations` root was provided for the open target definition.

Actions:

1. Add closed generic roots for each target instantiation that must be emitted.
2. Replace the open map rule with explicit closed mappings when the supported instantiations are fixed.

### Missing assembly resolution during generation

Cause:

1. Mapped assembly name not present in provided target/proxy inputs (either kind of input provides both sides).

Actions:

1. Add missing `--proxy-assembly` or expand `--target-folder`.
2. Add `--target-filter` for deterministic assembly closure.

## Recommended CI Gates

Use all of these gates together.

1. Dynamic baseline suite green:
   1. `DD_DUCKTYPE_TEST_MODE=dynamic ... Datadog.Trace.DuckTyping.Tests`
2. Full parity orchestration gate:
   1. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 ... DuckTypeAotFullSuiteParityIntegrationTests`
3. AOT processor and NativeAOT integration:
   1. `DD_RUN_DUCKTYPE_AOT_NATIVEAOT_PUBLISH=1 ... --filter FullyQualifiedName~DuckTypeAot`
4. AOT bootstrap performance guard:
   1. `DuckTypeAotRegistryBootstrapBenchmark`
5. Optional explicit verify-compat step against generated artifacts.

## Cross-Reference

1. Dynamic DuckTyping guide: [DuckTyping.md](./DuckTyping.md)
2. Complete DuckTyping behavior and examples: [DuckTyping.Bible.md](./DuckTyping.Bible.md)
3. NativeAOT build integration: [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md)
4. NativeAOT specification: [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md)
5. NativeAOT compatibility matrix: [DuckTyping.NativeAOT.CompatibilityMatrix.md](./DuckTyping.NativeAOT.CompatibilityMatrix.md)
6. NativeAOT troubleshooting: [DuckTyping.NativeAOT.Troubleshooting.md](./DuckTyping.NativeAOT.Troubleshooting.md)
7. NativeAOT testing playbook: [DuckTyping.NativeAOT.Testing.md](./DuckTyping.NativeAOT.Testing.md)
8. NativeAOT parity stabilization plan: [DuckTyping-NativeAOT-Parity-Stabilization-Plan.md](./for-ai/DuckTyping-NativeAOT-Parity-Stabilization-Plan.md)
