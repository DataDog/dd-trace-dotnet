# DuckTyping Bible (Datadog.Trace)

This document is a deep technical reference for the DuckTyping system used by `Datadog.Trace`.

It is intentionally long and explicit.

Goals:
- Explain what DuckTyping is doing at runtime.
- Enumerate each supported feature and variant.
- Show how proxies are generated and how they behave.
- Explain caches, dynamic modules, helper layers, and conversion rules.
- Provide test-inspired examples for practical usage.
- Document known limitations and known detection gaps.

This document is implementation-first. Existing documentation is useful, but this file follows current code and tests as source of truth.

## Table of Contents

1. [Scope and source of truth](#scope-and-source-of-truth)
2. [High-level architecture](#high-level-architecture)
3. [Execution modes](#execution-modes)
4. [Public surface area](#public-surface-area)
5. [Proxy shape and generated type model](#proxy-shape-and-generated-type-model)
6. [Feature catalog](#feature-catalog)
7. [Test evidence index](#test-evidence-index)
8. [Attribute reference](#attribute-reference)
9. [Method binding and overload resolution internals](#method-binding-and-overload-resolution-internals)
10. [Type conversion and duck chaining internals](#type-conversion-and-duck-chaining-internals)
11. [Visibility, access checks, and dynamic module strategy](#visibility-access-checks-and-dynamic-module-strategy)
12. [Cache system and lifecycle](#cache-system-and-lifecycle)
13. [IL helper and codegen helper layers](#il-helper-and-codegen-helper-layers)
14. [IL emission atlas (opcode-level)](#il-emission-atlas-opcode-level)
15. [Exception taxonomy](#exception-taxonomy)
16. [Analyzer and code fix](#analyzer-and-code-fix)
17. [Known limitations and known detection gaps](#known-limitations-and-known-detection-gaps)
18. [Best practices for contributors](#best-practices-for-contributors)
19. [Detailed examples](#detailed-examples)
20. [Feature-by-feature test-adapted excerpts](#feature-by-feature-test-adapted-excerpts)
21. [Source map](#source-map)

---

## Scope and source of truth

Primary implementation files:
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Methods.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Properties.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Fields.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Utilities.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Statics.cs`
- `tracer/src/Datadog.Trace/DuckTyping/ILHelpersExtensions.cs`
- `tracer/src/Datadog.Trace/DuckTyping/LazyILGenerator.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckTypeExtensions.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckTypeExceptions.cs`

Primary contracts and attributes:
- `tracer/src/Datadog.Trace/DuckTyping/IDuckType.cs`
- `tracer/src/Datadog.Trace/DuckTyping/IDuckTypeTask.cs`
- `tracer/src/Datadog.Trace/DuckTyping/IDuckTypeAwaiter.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAttributeBase.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckFieldAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckPropertyOrFieldAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckCopyAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAsClassAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckReverseMethodAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckIncludeAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckIgnoreAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/ValueWithType.cs`

Test suite and behavior oracle:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/`

Analyzer and code fix:
- `tracer/src/Datadog.Trace.Tools.Analyzers/DuckTypeAnalyzer/DuckTypeNullCheckAnalyzer.cs`
- `tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/DuckTypeAnalyzer/DuckTypeNullCheckCodeFixProvider.cs`

Note:
- `docs/development/DuckTyping.md` contains useful conceptual guidance, but parts are outdated.

---

## High-level architecture

DuckTyping is a runtime proxy generation system.

It takes:
- A proxy definition type (interface, class, abstract class, struct with `[DuckCopy]` pattern).
- A runtime target instance type.

It produces:
- A generated proxy type bound to `(proxy definition type, runtime target type)`.
- A cached activator delegate for fast creation of proxy instances.

Core sequence:
1. Resolve or create a `CreateTypeResult` from cache using `TypesTuple(proxyDefinitionType, targetType)`.
2. Build type in two phases:
   - Dry run (`dryRun: true`) for validation and deterministic failure capture.
   - Real emit (`dryRun: false`) only if dry run succeeds.
3. Create proxy instance through cached delegate.

Core design priorities:
- Fast steady-state invocation after first generation.
- Minimal allocations in hot paths.
- Ability to access non-public members/types via controlled dynamic assembly strategy.
- Strong failure surfacing through specific `DuckType*Exception` classes.

---

## Execution modes

DuckTyping has three practical execution modes.

### 1. Forward proxy mode

Used by:
- `DuckType.Create<T>(instance)`
- `instance.DuckCast<T>()`
- `instance.TryDuckCast<T>(out var value)`

Behavior:
- Proxy definition describes how to view/call members of target.
- Generated proxy delegates member operations to target instance.

### 2. Reverse proxy mode

Used by:
- `DuckType.CreateReverse(typeToDeriveFrom, delegationInstance)`
- `delegationInstance.DuckImplement(typeToDeriveFrom)`
- `delegationInstance.TryDuckImplement(typeToDeriveFrom, out var value)`

Behavior:
- Generated type derives from or implements `typeToDeriveFrom`.
- Method/property implementations are delegated to `delegationInstance` members marked with `[DuckReverseMethod]`.
- Signature rules are strict.

### 3. DuckCopy struct projection mode

Used when proxy definition is a struct (usually `[DuckCopy]`).

Behavior:
- Runtime-generated helper proxy reads members from target instance.
- Values are copied into fields of the output struct.
- This is copy/project behavior, not mutable delegation.

---

## Public surface area

`DuckType` core API:
- `Create<T>(object? instance)`
- `Create(Type proxyType, object instance)`
- `CanCreate<T>(object? instance)`
- `CanCreate(Type proxyType, object instance)`
- `CreateReverse(Type typeToDeriveFrom, object delegationInstance)`
- `GetOrCreateProxyType(Type proxyType, Type targetType)`
- `GetOrCreateReverseProxyType(Type typeToDeriveFrom, Type delegationType)`

`DuckTypeExtensions` convenience API:
- `DuckCast<T>(this object? instance)`
- `DuckCast(this object instance, Type targetType)`
- `TryDuckCast<T>(this object? instance, out T? value)`
- `TryDuckCast(this object? instance, Type targetType, out object? value)`
- `DuckAs<T>(this object? instance) where T : class`
- `DuckAs(this object? instance, Type targetType)`
- `DuckIs<T>(this object? instance)`
- `DuckIs(this object? instance, Type targetType)`
- `DuckImplement(this object instance, Type typeToDeriveFrom)`
- `TryDuckImplement(this object? instance, Type typeToDeriveFrom, out object? value)`

### Null semantics summary

- `DuckType.Create(Type, object)` and related non-null core APIs: throw if instance is null.
- Generic `DuckType.Create<T>(object?)`: returns `default` when instance is null.
- Extension `DuckCast<T>`: returns `default` on null.
- Extension `DuckAs<T>`: returns `null` on null or on non-creatable mapping.
- Extension `TryDuckCast`/`TryDuckImplement`: return `false` and `value = default` on null/failure.

---

## Proxy shape and generated type model

All generated proxies implement `IDuckType`.

`IDuckType` members:
- `object? Instance { get; }`
- `Type Type { get; }`
- `ref TReturn? GetInternalDuckTypedInstance<TReturn>()`
- `string ToString()` (forwarded behavior)

Generated proxy has a private readonly `_currentInstance` field.

### Interface proxy default shape

If proxy definition is an interface and not marked `[DuckAsClass]`, generated proxy is a value type (`struct`-like emitted type) implementing:
- the interface
- `IDuckType`

Pseudo shape:

```csharp
public struct GeneratedProxy : IMyProxy, IDuckType
{
    private readonly TTarget _currentInstance;

    public object Instance => _currentInstance;
    public Type Type => typeof(TTarget);

    public string Name => _currentInstance.Name;
}
```

### Interface with `[DuckAsClass]`

If interface has `[DuckAsClass]`, generated proxy becomes class-like shape.

### Abstract or virtual class proxy shape

Generated type derives from proxy class and overrides abstract/virtual members.

Pseudo shape:

```csharp
public sealed class GeneratedProxy : MyAbstractProxyBase, IDuckType
{
    private readonly TTarget _currentInstance;

    public override string Name => _currentInstance.Name;
}
```

### Reverse proxy shape

Generated type derives from target base/interface and routes calls to delegation instance.

Pseudo shape:

```csharp
public sealed class GeneratedReverseProxy : ThirdPartyBase, IDuckType
{
    private readonly MyImplementation _currentInstance;

    public override string GetValue() => _currentInstance.GetValueReverse();
}
```

### DuckCopy struct shape

DuckCopy mode creates projection code that reads target members and stores values into struct fields.

Pseudo shape:

```csharp
[DuckCopy]
public struct MyProjection
{
    public string Name;
    public int Count;
}
```

Generated runtime helper:
- Reads target values via generated property getters.
- Writes fields into `MyProjection` local and returns it.

---

## Feature catalog

This section enumerates supported features one by one.

### A. Member access features

1. Property get mapping by name
- Default behavior when property names match.

2. Property get mapping with rename
- `[Duck(Name = "OtherName")]`.

3. Property set mapping
- Supports setter if target member writable.
- Fails with `DuckTypePropertyCantBeWrittenException` if not writable.

4. Indexer property support
- Indexer signatures are supported.
- Parameter count mismatch throws `DuckTypePropertyArgumentsLengthException`.

5. Field get mapping
- Use `[DuckField(Name = "_fieldName")]` or `DuckKind.Field`.

6. Field set mapping
- Fails on readonly fields with `DuckTypeFieldIsReadonlyException`.

7. Property-or-field fallback mapping
- `[DuckPropertyOrField]` or `DuckKind.PropertyOrField`.
- Property is checked first, field second.

8. Comma-separated name fallbacks
- `Name = "newName,oldName"` style supported for field/property/method resolution.

9. Static member mapping
- Static fields/properties/methods are supported.

10. Multiple visibility levels
- Public/internal/private target members can be accessed through dynamic access strategy.

11. Ignore member
- `[DuckIgnore]` excludes method/property/field from proxy emission.

12. Include object-level method
- `[DuckInclude]` allows explicitly including methods normally skipped (for example `GetHashCode`).

13. Object method behavior
- Methods declared on `object` are skipped unless explicitly included.

14. `ToString()` behavior
- Generated proxy has `ToString` forwarding to underlying instance with null-safe behavior for reference targets.

15. Struct member mutation guard
- Writing to members declared on value type target is blocked (`DuckTypeStructMembersCannotBeChangedException`).

### B. Method binding features

16. Straight method name binding
- Match by same method name and compatible signature.

17. Rename method binding
- `[Duck(Name = "MethodOnTarget")]`.

18. Overload support
- Overload selected by exact signature first.

19. Optional parameters support
- Target methods with optional trailing parameters can be called with fewer proxy parameters.

20. `ref` and `out` parameters
- Supported with signature checks.
- Different element types can be bridged via locals + conversion + post-call assignment.

21. Parameter type-name disambiguation
- `[Duck(ParameterTypeNames = ...)]` supports exact selection and disambiguation.

22. Generic method support (public path)
- Generic proxy methods can map generic target methods in direct-call mode.

23. Generic method support for non-public target with explicit type names
- Non-generic proxy method can bind generic target method using `GenericParameterTypeNames`.

24. Generic + non-public dynamic method limitation
- Generic proxy method against non-public instances is rejected.

25. Explicit interface method binding
- `ExplicitInterfaceTypeName` supports explicit interface implementation mapping.
- Wildcard `"*"` supports relaxed explicit name matching.

26. Ambiguous method detection
- Multiple candidate matches produce `DuckTypeTargetMethodAmbiguousMatchException`.

27. Reverse method mapping with `[DuckReverseMethod]`
- Reverse mode uses dedicated attribute and strict implementation checks.

### C. Duck chaining and typed wrapping features

28. Nested proxy chaining for return values
- If target returns unknown type and proxy expects another proxy type, DuckType wraps automatically.

29. Nested proxy extraction for input arguments
- If proxy receives duck proxy and target expects original type, `IDuckType.Instance` extraction is emitted.

30. Reverse chaining for reverse proxies
- Reverse flow can create reverse proxies for return/input conversions.

31. Nullable duck chaining support
- `Nullable<T>` with duck copy generic argument has dedicated IL path.

32. `ValueWithType<T>` wrapper support
- Return and parameter wrappers preserve original runtime type metadata.

33. Enum handling in conversion checks
- Conversion logic normalizes enum underlying types.

### D. DuckCopy struct projection features

34. Struct projection from target object
- Proxy struct fields are filled from target members.

35. Non-public member reading in projection
- Supported through same visibility strategy.

36. Projection with nested duck chaining
- Struct fields can be duck-chained when needed.

37. Guard: empty `[DuckCopy]` struct
- If struct has properties but no usable fields, throws `DuckTypeDuckCopyStructDoesNotContainsAnyField`.

### E. Reverse proxy-specific features

38. Override/implementation generation for abstract/virtual members
- Only abstract/virtual/eligible methods and properties can be overridden.

39. Required implementation checks
- Missing required abstract members produce reverse missing implementation exceptions.

40. Reverse generic contract strictness
- Generic target method must be implemented as generic with matching count.

41. Reverse custom attribute copy
- Custom attributes from implementation type are copied onto generated reverse proxy type.
- Attributes with named arguments are rejected.

42. Reverse type constraints
- Cannot reverse-proxy a struct base type.
- Delegation implementor cannot be interface or abstract.

---

## Test evidence index

This section maps major feature families to representative tests.

Core extension/API semantics:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckTypeExtensionsTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs`

Interface/class/struct proxy shape behavior:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckAsClassTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/StructTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/GetAssemblyTests.cs`

Field mapping:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Fields/ValueType/ValueTypeFieldTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Fields/ReferenceType/ReferenceTypeFieldTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Fields/TypeChaining/TypeChainingFieldTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Fields/ValueType/ValueTypeFieldErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Fields/ReferenceType/ReferenceTypeFieldErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Fields/TypeChaining/TypeChainingFieldErrorTests.cs`

Property mapping and indexers:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Properties/ValueType/ValueTypePropertyTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Properties/ReferenceType/ReferenceTypePropertyTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Properties/TypeChaining/TypeChainingPropertyTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Properties/ValueType/ValueTypePropertyErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Properties/ReferenceType/ReferenceTypePropertyErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Properties/TypeChaining/TypeChainingPropertyErrorTests.cs`

Property-or-field fallback:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/PropertyOrFieldTests.cs`

Method binding, overloads, generics, ref/out:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Methods/MethodTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Methods/NonGenerics/NonGenericMethodErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Methods/Generics/GenericMethodErrorTests.cs`

Reverse proxy:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ReverseProxyTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/ReverseProxy/ReverseProxyErrorTests.cs`

Explicit interface binding:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckExplicitInterfaceTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckExplicitInterfacePrivateTests.cs`

Behavior toggles and helper attributes:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckIgnoreTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckIncludeTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckToStringByPassTests.cs`

Name fallback and compatibility edge cases:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/MultipleNamesTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/LongTypeNameTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/PrivateGenericTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckChainingNullableTests.cs`

---

## Attribute reference

### `DuckAttributeBase`

Properties:
- `Name`
- `BindingFlags` (default: `Public | NonPublic | Static | Instance | FlattenHierarchy`)
- `GenericParameterTypeNames`
- `ParameterTypeNames`
- `ExplicitInterfaceTypeName`

Used by:
- `DuckAttribute`
- `DuckReverseMethodAttribute`

### `DuckAttribute`

Adds:
- `Kind` (`Property`, `Field`, `PropertyOrField`)

### `DuckFieldAttribute`

Equivalent to `DuckAttribute` with `Kind = Field`.

### `DuckPropertyOrFieldAttribute`

Equivalent to `DuckAttribute` with `Kind = PropertyOrField`.

### `DuckCopyAttribute`

Marks struct projection type.

Optional metadata:
- `TargetType`
- `TargetAssembly`

### `DuckAsClassAttribute`

Applied to interface proxy definition to force class proxy shape.

### `DuckReverseMethodAttribute`

Reverse-proxy mapping attribute for methods/properties on delegation implementor.

### `DuckIncludeAttribute`

Force include members otherwise skipped (notably object-level behavior).

### `DuckIgnoreAttribute`

Exclude specific member from duck processing.

### `DuckTypeAttribute`

Marker attribute indicating type is intended for duck typing use.

### `DuckTypeTarget`

Attribute for method/property/field/constructor indicating instrumentation target usage.

---

## Method binding and overload resolution internals

Method selection is done by `SelectTargetMethod<TAttribute>()`.

Selection process:
1. Load duck attribute (`DuckAttribute` or `DuckReverseMethodAttribute`) and derive name.
2. If `ParameterTypeNames` provided and fully resolvable, attempt exact method lookup with those types.
3. Try direct lookup with proxy parameter CLR types.
4. Fallback to candidate scan (`allTargetMethods`) with compatibility heuristics:
   - Name match or explicit-interface transformed name.
   - Optional wildcard explicit-interface matching.
   - Direction checks (`in/out/ref`) must match.
   - ByRef shape must match.
   - Parameter compatibility checks for value/class/generic scenarios.
   - Optional trailing parameter handling.
5. If more than one candidate passes: ambiguous exception.

Special reverse rule:
- If `[DuckReverseMethod(ParameterTypeNames)]` count differs from method parameter count, throws `DuckTypeReverseAttributeParameterNamesMismatchException`.

Generic handling details:
- Forward path allows mapping to generic target methods.
- For non-public instances, generic proxy methods are disallowed in dynamic path.
- Non-generic proxy to generic target is supported if `GenericParameterTypeNames` are provided and resolvable.

---

## Type conversion and duck chaining internals

Conversion logic lives in `ILHelpersExtensions.WriteTypeConversion` and `CheckTypeConversion`.

### Conversion behavior matrix

1. Underlying types equal
- No conversion emitted.

2. Value type -> value type
- Must match exactly.
- Otherwise `DuckTypeInvalidTypeConversionException`.

3. Value type -> object/interface
- Box + cast if assignable.
- Error if not assignable.

4. Object/interface -> value type
- Runtime `isinst`/unbox safety path when possible.
- Error if conversion cannot be valid.

5. Class -> class/interface
- `castclass` emitted where needed.

6. Enum support
- Underlying enum types are compared for conversion checks.

### Duck chaining decision

`NeedsDuckChaining(targetType, proxyType)` returns true when at least one condition applies:
- Proxy type has `[DuckCopy]`.
- Types differ and proxy type is non-value, non-generic-parameter, non-base/interface assignable, and non-CLR module type.
- Proxy type is `Nullable<T>` where `T` has `[DuckCopy]`.

### Forward chaining

`AddIlToDuckChain` emits calls to:
- `CreateCache<T>.CreateFrom<TOriginal>` for value-type originals.
- `CreateCache<T>.Create` in standard cases.
- Nullable-specific wrapper creation path for `Nullable<T>`.

### Reverse chaining

`AddIlToDuckChainReverse` emits `CreateCache<T>.CreateReverse`.

### Duck instance extraction

`AddIlToExtractDuckType` casts to `IDuckType` and reads `.Instance`.

---

## Visibility, access checks, and dynamic module strategy

DuckTyping uses a hybrid strategy to deal with member visibility.

### Direct access path

When direct access is possible, emitted code uses direct field/property/method access.

Method calls:
- Public methods: normal `call`/`callvirt`.
- Non-public methods (when directly reachable target type): `calli` with function pointer.

### Dynamic method path

When target visibility blocks direct emission:
- Emit `DynamicMethod` with object-based signature bridge.
- Convert arguments in bridge.
- Invoke actual target call inside dynamic method.
- Convert return value back.

### Dynamic assembly/module strategy (`GetModuleBuilder`)

Three module creation modes:
1. Target not visible
- Dedicated module using `DuckTypeNotVisibleAssemblyPrefix`.
- Reason: changing `IgnoresAccessChecksTo` set means module cannot be safely reused.

2. Generic target with arguments from multiple assemblies
- Dedicated module using `DuckTypeGenericTypeAssemblyPrefix`.

3. Visible target and compatible generic context
- Reuse per-target-assembly module from `ActiveBuilders`.
- Name prefix `DuckTypeAssemblyPrefix`.

### Visibility expansion (`EnsureTypeVisibility`)

Algorithm:
1. Traverse input type and non-visible related types (generic arguments, nesting chain).
2. Collect assembly names.
3. For each new assembly name not yet added for that module:
   - Attach assembly-level `IgnoresAccessChecksToAttribute(assemblyName)`.

This allows access to internals/private metadata in generated dynamic assembly context (runtime permitting).

---

## Cache system and lifecycle

### Global type cache

`DuckTypeCache`:
- Type: `ConcurrentDictionary<TypesTuple, Lazy<CreateTypeResult>>`
- Key: `TypesTuple(proxyDefinitionType, targetType)`
- Value: lazy result for deterministic single creation per key.

`TypesTuple` hash uses FNV-like unchecked composition of both type hash codes.

### Generic fast path cache

`CreateCache<T>` has static `_fastPath` (`StrongBox<CreateTypeResult>`):
- Optimizes first-seen target type for proxy definition `T`.
- Useful because many proxy definitions map to one dominant target type.

### CreateTypeResult lifecycle

`CreateTypeResult` stores:
- `Success` status
- `TargetType`
- Generated `ProxyType` (if success)
- Activator delegate (if success)
- Captured `ExceptionDispatchInfo` (if failure)

Failure behavior:
- Result still cached.
- Later `CreateInstance`/`ProxyType` access rethrows original captured exception preserving stack semantics.

### Two-phase create flow

1. Dry run:
- `CreateProxyType(..., dryRun: true)` or reverse equivalent.
- Performs full binding and conversion validations without final emitted type.

2. Real run:
- Executed only when dry run can create successfully.

### Synchronization

- Build paths lock on global `Locker` for type emission sequence consistency.
- Cache itself is concurrent.
- Dynamic method list and ignore-access set dictionary use local locking.

---

## IL helper and codegen helper layers

### `LazyILGenerator`

`LazyILGenerator` stores IL operations as delayed actions.

Why it exists:
- Enables insertion (`SetOffset`) and delayed composition before flush.
- Helps for operations that need to inject pre-call delegate loads.
- Dry-run friendly (works with null generator).

### `ILHelpersExtensions`

Provides:
- Parameter/local opcode helpers (`WriteLoadArgument`, `WriteLoadLocal`, `WriteStoreLocal`).
- Constant emission helper (`WriteInt`).
- Conversion helpers (`WriteTypeConversion`, `CheckTypeConversion`).
- Function pointer call emission (`WriteMethodCalli`).
- Dynamic method delegate generation and invocation injection (`WriteDynamicMethodCall`).

### Delegate cache for dynamic method calls

`DuckType.DelegateCache<TDelegate>`:
- Stores static delegate instance for generated dynamic method.
- `WriteDynamicMethodCall` creates a runtime delegate type in the same module and stores delegate in cache.
- IL emits `GetDelegate()` call and then delegate `Invoke`.

---

## IL emission atlas (opcode-level)

This section documents how IL is emitted for the major runtime scenarios.

Important:
- Snippets are representative IL-style pseudocode.
- Exact local indices and label names vary.
- Branches are described exactly as encoded in the implementation.

Primary implementation locations:
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Methods.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Properties.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Fields.cs`
- `tracer/src/Datadog.Trace/DuckTyping/ILHelpersExtensions.cs`
- `tracer/src/Datadog.Trace/DuckTyping/LazyILGenerator.cs`

### Core opcode vocabulary used by DuckTyping

- `ldarg.*`: load argument.
- `ldloc.*` / `stloc.*`: load/store local.
- `ldfld` / `ldsfld`: load instance/static field.
- `stfld` / `stsfld`: store instance/static field.
- `ldflda`: load field address (used for value-type receiver calls).
- `call` / `callvirt`: direct method invocation.
- `calli`: function-pointer call (used for non-public method/property access in direct call path).
- `castclass`: reference-type cast.
- `unbox.any` / `unbox`: value-type extraction from object.
- `box`: value-to-object boxing.
- `isinst`: runtime type test/cast probe.
- `ldtoken` + `Type.GetTypeFromHandle`: runtime `Type` materialization.
- `newobj`: create object/struct wrapper (including `Nullable<T>` wrappers).
- `initobj`: initialize value-type local to default.
- `ldind.ref` / `stind.ref`: load/store through byref pointer (ref/out bridges).
- branching opcodes (`brtrue.s`, `br.s`, labels): null checks and conversion guards.

### Common emitted prelude for proxy methods (forward and reverse)

Implemented in `MethodIlHelper.InitialiseProxyMethod`.

Representative pattern for instance target method:

```il
// load generated proxy instance ("this")
ldarg.0

// load _currentInstance (type-specific field created in generated proxy)
// value-type target -> address; reference-type target -> object ref
ldflda _currentInstance   // when value type
// or
ldfld  _currentInstance   // when reference type
```

For static target methods, receiver load is omitted.

### Forward properties: getter emission matrix

Implemented by `GetPropertyGetMethod`.

#### Case FG-1: Instance property getter, public target getter, reference-type target

```il
ldarg.0
ldfld _currentInstance
// indexer args (if any), converted as needed
callvirt instance <TProp> TargetType::get_Property(...)
// optional duck chain or conversion
ret
```

#### Case FG-2: Instance property getter, public target getter, value-type target

```il
ldarg.0
ldflda _currentInstance
call instance <TProp> ValueTargetType::get_Property(...)
// optional duck chain or conversion
ret
```

#### Case FG-3: Instance/static property getter, non-public target getter

DuckTyping uses `calli` for non-public property methods in direct path.

```il
// receiver load for instance target if needed
...
ldc.i8 <function_ptr_of_getter>
conv.i
calli <calling-convention> <return-type> (<arg-types>)
// optional duck chain or conversion
ret
```

#### Case FG-4: Static property getter

No receiver prelude:

```il
// load index args if any
call    <TProp> TargetType::get_StaticProperty(...)
// or calli for non-public
ret
```

#### Case FG-5: Getter returning `ValueWithType<T>`

After return value is on stack:

```il
ldtoken <actual_runtime_return_type>
call class [System.Runtime]System.Type System.Type::GetTypeFromHandle(valuetype System.RuntimeTypeHandle)
call valuetype Datadog.Trace.DuckTyping.ValueWithType`1<!T> Datadog.Trace.DuckTyping.ValueWithType`1<!T>::Create(!T, class System.Type)
ret
```

#### Case FG-6: Getter duck chaining (forward)

When `NeedsDuckChaining(targetPropertyType, proxyPropertyType)` is true:

```il
// value from target is on stack
call !TProxy Datadog.Trace.DuckTyping.DuckType/CreateCache`1<!TProxy>::Create(object)
ret
```

For value-type source objects, emitted call is `CreateFrom<TOriginal>`.

#### Case FG-7: Getter duck chaining (reverse property flow)

In reverse property mapping, getter conversion function is swapped:

```il
// value from implementation property is on stack
castclass Datadog.Trace.DuckTyping.IDuckType
callvirt instance object Datadog.Trace.DuckTyping.IDuckType::get_Instance()
// then conversion to outer property type
ret
```

#### Case FG-8: Getter indexer argument conversion and duck extraction

For each indexer arg:
- if duck chaining needed for arg type, emitted sequence includes cast to `IDuckType` and `get_Instance`.
- then `WriteTypeConversion`.

Representative shape:

```il
ldarg.<n>
castclass Datadog.Trace.DuckTyping.IDuckType
callvirt instance object Datadog.Trace.DuckTyping.IDuckType::get_Instance()
// convert object -> target index arg type
...
```

#### Case FG-9: Dynamic-method fallback path for property getter

Dynamic fallback branch exists in code and emits:
- an inner `DynamicMethod` that loads/converts receiver and args;
- inner call/callvirt to actual getter;
- conversion to dynamic return signature;
- outer IL invokes delegate via `WriteDynamicMethodCall`.

Inner method representative shape:

```il
// dyn receiver (object) -> target declaring type
ldarg.0
castclass TargetDeclaringType   // or unbox logic when needed

// dyn args converted to target indexer types
ldarg.1
...

callvirt instance <TProp> TargetDeclaringType::get_Property(...)
// convert to dyn return type if needed
ret
```

### Forward properties: setter emission matrix

Implemented by `GetPropertySetMethod`.

#### Case FS-1: Instance property setter, public target setter

```il
ldarg.0
ldfld/ldflda _currentInstance
// load indexer args + value arg
// value may be unwrapped from ValueWithType<T>
callvirt instance void TargetType::set_Property(...)
ret
```

#### Case FS-2: Non-public property setter

Uses `calli` in direct path:

```il
...
ldc.i8 <function_ptr_of_setter>
conv.i
calli <calling-convention> void (<arg-types>)
ret
```

#### Case FS-3: Setter argument duck extraction (forward)

When setter expects original type but proxy arg is duck type:

```il
ldarg.<value>
// optional: extract .Value from ValueWithType<T>
castclass Datadog.Trace.DuckTyping.IDuckType
callvirt instance object Datadog.Trace.DuckTyping.IDuckType::get_Instance()
// convert object -> target parameter type
```

#### Case FS-4: Setter argument duck creation (reverse)

Reverse property mapping swaps direction and may emit `Create`/`CreateReverse` chain calls for setter argument adaptation.

#### Case FS-5: Static setter

No receiver prelude; only args emitted.

#### Case FS-6: Dynamic-method fallback path for property setter

Branch exists and emits dynamic bridge similarly to getter path.

### Forward fields: getter/setter emission matrix

Implemented by `GetFieldGetMethod` and `GetFieldSetMethod`.

#### Case FF-1: Instance field get (any visibility)

```il
ldarg.0
ldfld/ldflda _currentInstance
ldfld TargetType::<field>
// optional duck chain / conversion / ValueWithType wrap
ret
```

#### Case FF-2: Static field get

```il
ldsfld TargetType::<static_field>
// optional conversion
ret
```

#### Case FF-3: Instance field set

```il
ldarg.0
ldfld/ldflda _currentInstance
ldarg.1
// optional ValueWithType value extraction
// optional IDuckType.Instance extraction for chaining
// conversion
stfld TargetType::<field>
ret
```

#### Case FF-4: Static field set

```il
ldarg.1
// conversion
stsfld TargetType::<static_field>
ret
```

#### Case FF-5: Dynamic-method fallback path for fields

Code includes dynamic bridge emission for non-direct path:
- getter dynamic method emits `ldfld/ldsfld` and conversion;
- setter dynamic method emits `stfld/stsfld` after conversion.

### Forward methods: invocation matrix

Implemented by `CreateMethods` + `MethodIlHelper`.

#### Case FM-1: Instance method, public target method, reference-type receiver

```il
ldarg.0
ldfld _currentInstance
// parameter load+conversion
callvirt instance <TReturn> TargetType::Method(...)
// output/ref fixups
// return conversion / duck chain / ValueWithType wrap
ret
```

#### Case FM-2: Instance method, public target method, value-type receiver

```il
ldarg.0
ldflda _currentInstance
// args
call instance <TReturn> ValueTargetType::Method(...)
...
ret
```

#### Case FM-3: Static method, public target method

```il
// args
call <TReturn> TargetType::Method(...)
...
ret
```

#### Case FM-4: Non-public target method

Direct call path emits `calli` through function pointer:

```il
...
ldc.i8 <function_ptr>
conv.i
calli <calling-convention> <return-type> (<arg-types>)
...
ret
```

#### Case FM-5: Generic target method (direct path)

`MakeGenericMethod` is emitted before call when proxy method has generic parameters.

#### Case FM-6: Dynamic-method fallback path for methods

When non-direct path is selected, `AddIlForDynamicMethodCall` emits:

1. Inner dynamic method:
```il
// optional receiver load+cast from object
// arg load+conversion
call/callvirt or calli (when ContainsGenericParameters)
// return conversion to dyn return signature
ret
```

2. Outer proxy method:
```il
call class DelegateType DuckType.DelegateCache`1<DelegateType>::GetDelegate()
// load args
callvirt instance <TReturn> DelegateType::Invoke(...)
```

#### Case FM-7: Parameter bridging for `ref`/`out` with type mismatch

Forward method emission creates locals and write-back logic.

Out mismatch representative shape:

```il
// before call: pass local byref to target
ldloca.s localTargetOut

// after call:
ldarg.<outArg>
ldloc.s localTargetOut
// optional duck chain/conversion
stind.ref
```

Ref mismatch representative shape:

```il
// read proxy ref arg
ldarg.<refArg>
ldind.ref
// optional duck extraction/chaining
// conversion
stloc.s localRef
ldloca.s localRef

// after call write-back:
ldarg.<refArg>
ldloc.s localRef
// optional conversion/chaining
stind.ref
```

#### Case FM-8: Optional parameter handling

Method selection allows candidates with optional trailing parameters.
Emission enforces required parameter presence and validates optional signature compatibility.

### Return IL matrix (methods and property-like returns)

Implemented by `TryAddReturnIl`.

#### Case RT-1: void-to-void

```il
ret
```

#### Case RT-2: void mismatch

Return IL synthesis fails and caller throws `DuckTypeProxyAndTargetMethodReturnTypeMismatchException`.

#### Case RT-3: conversion-only return

```il
// current return value on stack
// WriteSafeTypeConversion(currentReturnType, outerReturnType)
ret
```

#### Case RT-4: duck-chain return

```il
// current return value on stack
call CreateCache<OuterReturnType>.Create(...)      // forward
// or
call CreateCache<OuterReturnType>.CreateReverse(...) // reverse
ret
```

#### Case RT-5: `ValueWithType<T>` return envelope

```il
// value on stack
ldtoken <innerMethodReturnType>
call Type.GetTypeFromHandle
call ValueWithType<T>.Create(value, type)
ret
```

### Reverse methods: IL differences from forward methods

Implemented by `CreateReverseProxyMethods`.

Differences:
- Outer method = overridden/implemented method on base contract.
- Inner method = delegation class method marked `[DuckReverseMethod]`.
- Parameter chaining direction is reversed:
  - load path uses `AddIlToDuckChain` + `NeedsDuckChainingReverse`.
  - write-back path uses `AddIlToExtractDuckType`.
- Return chaining direction uses `AddIlToDuckChainReverse`.
- Reverse path always uses direct method-call helper (`AddIlForDirectMethodCall`).

Representative pattern:

```il
ldarg.0
ldfld _currentInstance   // delegation object
// args adapted from outer signature to delegation signature
callvirt instance <TReturnInner> DelegationType::Method(...)
// ref/out write-back with reverse extraction
// return adapted back to overridden method return type
ret
```

### Reverse properties: IL differences from forward properties

Reverse property emission reuses getter/setter builders with swapped chain delegates:
- Getter uses `AddIlToExtractDuckType` (inner -> outer extraction).
- Setter uses `AddIlToDuckChain` (outer -> inner wrapping when needed).
- Chaining predicate is `NeedsDuckChainingReverse`.

### Static vs instance: opcode differences summary

#### Instance members

- receiver prelude present (`ldarg.0` + `_currentInstance` load).
- value-type receiver commonly uses `ldflda`.
- reference-type receiver uses `ldfld`.

#### Static members

- no receiver prelude.
- field ops use `ldsfld/stsfld`.
- method/property ops use static `call`/`calli`.

### Visibility matrix (public/internal/private)

#### Fields

- Public/internal/private fields are loaded/stored with `ldfld/stfld` (or static variants) when direct path is active.
- Access is enabled via dynamic assembly visibility strategy (`IgnoresAccessChecksToAttribute`).

#### Property and method accessors

- Public accessors/methods: `call` or `callvirt`.
- Non-public accessors/methods: `calli` with function pointer.

### Combination tables (forward and reverse)

These tables make the branch behavior explicit across visibility and member shape.

#### Table CM-1: Forward property getter/setter combinations

| Member kind | Static/Instance | Visibility | Receiver load | Invocation opcode path | Notes |
|---|---|---|---|---|---|
| Property get | Instance | Public | `ldarg.0` + `_currentInstance` (`ldfld`/`ldflda`) | `callvirt` (reference target) or `call` (value target) | Index args converted first |
| Property get | Instance | Internal | same | `calli` | Function pointer from accessor method handle |
| Property get | Instance | Private | same | `calli` | Same non-public branch |
| Property get | Static | Public | none | `call` | No receiver |
| Property get | Static | Internal | none | `calli` | Non-public static accessor |
| Property get | Static | Private | none | `calli` | Non-public static accessor |
| Property set | Instance | Public | `ldarg.0` + `_currentInstance` | `callvirt` (reference target) or `call` (value target) | Setter args converted first |
| Property set | Instance | Internal | same | `calli` | Non-public setter path |
| Property set | Instance | Private | same | `calli` | Non-public setter path |
| Property set | Static | Public | none | `call` | No receiver |
| Property set | Static | Internal | none | `calli` | Non-public static setter |
| Property set | Static | Private | none | `calli` | Non-public static setter |

#### Table CM-2: Forward field getter/setter combinations

| Member kind | Static/Instance | Visibility | Receiver load | Field opcode path | Notes |
|---|---|---|---|---|---|
| Field get | Instance | Public | `ldarg.0` + `_currentInstance` | `ldfld` | direct path via visibility strategy |
| Field get | Instance | Internal | same | `ldfld` | same emitted path |
| Field get | Instance | Private | same | `ldfld` | same emitted path |
| Field get | Static | Public | none | `ldsfld` | static read |
| Field get | Static | Internal | none | `ldsfld` | static read |
| Field get | Static | Private | none | `ldsfld` | static read |
| Field set | Instance | Public | `ldarg.0` + `_currentInstance` | `stfld` | arg converted before store |
| Field set | Instance | Internal | same | `stfld` | same emitted path |
| Field set | Instance | Private | same | `stfld` | same emitted path |
| Field set | Static | Public | none | `stsfld` | static write |
| Field set | Static | Internal | none | `stsfld` | static write |
| Field set | Static | Private | none | `stsfld` | static write |

#### Table CM-3: Forward method invocation combinations

| Method kind | Static/Instance | Visibility | Receiver load | Invocation opcode path | Notes |
|---|---|---|---|---|---|
| Method call | Instance | Public | `ldarg.0` + `_currentInstance` | `callvirt` (reference target) or `call` (value target) | generic allowed in direct branch |
| Method call | Instance | Internal | same | `calli` | non-public branch in `AddIlForDirectMethodCall` |
| Method call | Instance | Private | same | `calli` | same |
| Method call | Static | Public | none | `call` | static dispatch |
| Method call | Static | Internal | none | `calli` | static non-public |
| Method call | Static | Private | none | `calli` | static non-public |

#### Table CM-4: Reverse method/property combinations

Reverse flow maps outer contract members to delegation members marked `[DuckReverseMethod]`.

| Reverse member | Static/Instance on delegation side | Visibility on delegation member | Opcode path | Notes |
|---|---|---|---|---|
| Reverse method | Instance | Public | direct `callvirt`/`call` | reverse builder scans instance methods only |
| Reverse method | Instance | Internal | direct `calli` for non-public | follows direct-call helper behavior |
| Reverse method | Instance | Private | skipped by method selection (private methods not included) | reverse implementor methods are from `DeclaredOnly` instance set, then filtered by matching logic |
| Reverse property get/set | Instance | Public | direct accessor call path | chain direction reversed |
| Reverse property get/set | Instance | Internal | accessor `calli` path for non-public | chain direction reversed |
| Reverse property get/set | Instance | Private | mapping fails unless resolvable through configured reflection path | depends on selected member discoverability |

#### Table CM-5: Type visibility vs generation behavior

| Target type visibility/profile | Module strategy | Access strategy during emit | Expected call style |
|---|---|---|---|
| Public/nested public, same-assembly generic args | reusable assembly module (`DuckTypeAssemblyPrefix`) | `EnsureTypeVisibility` + direct emit | direct + `call`/`callvirt`/`calli` |
| Non-visible target type | dedicated module (`DuckTypeNotVisibleAssemblyPrefix`) | `EnsureTypeVisibility` + direct emit | direct + non-public via `calli` |
| Generic type with args from multiple assemblies | dedicated module (`DuckTypeGenericTypeAssemblyPrefix`) | visibility expansion per needed assemblies | direct emit style |

### Value-type vs reference-type receiver combinations

Receiver load is one of the most important emitted differences.

| Receiver kind | Load sequence | Why |
|---|---|---|
| Reference target instance | `ldarg.0` + `ldfld _currentInstance` | object reference receiver |
| Value target instance | `ldarg.0` + `ldflda _currentInstance` | address receiver needed for struct instance calls |
| Static member | none | no receiver |

### Forward vs reverse chaining combinations

| Flow | Input/output adaptation helper | Typical emitted effect |
|---|---|---|
| Forward parameter adaptation | `AddIlToExtractDuckType` | `castclass IDuckType` + `get_Instance` |
| Forward return adaptation | `AddIlToDuckChain` | `CreateCache<T>.Create` / `CreateFrom<TOriginal>` |
| Reverse parameter adaptation | `AddIlToDuckChain` + `NeedsDuckChainingReverse` | wraps outer arg into inner proxy shape |
| Reverse return adaptation | `AddIlToDuckChainReverse` | `CreateCache<T>.CreateReverse` |
| Reverse ref/out write-back | `AddIlToExtractDuckType` | unwraps back to outer contract value |

### Dynamic method delegate injection mechanics (when dynamic path is used)

Implemented by `WriteDynamicMethodCall`.

Pattern:
1. Build a runtime delegate type in same module as proxy.
2. Store `DynamicMethod` in global indexed list.
3. Fill `DuckType.DelegateCache<DelegateType>` with constructed delegate.
4. Insert IL at method start:
   - `call DelegateCache<DelegateType>.GetDelegate()`
5. Emit `callvirt DelegateType::Invoke` with original arguments.

Representative outer IL:

```il
call class DelegateType DuckType/DelegateCache`1<DelegateType>::GetDelegate()
ldarg.0
ldarg.1
...
callvirt instance <TReturn> DelegateType::Invoke(...)
```

### Conversion IL deep-dive (selected branches)

#### Object/interface -> value type guarded unbox

Representative sequence from conversion helper:

```il
dup
isinst <ExpectedValueType>
brtrue.s L_ok
pop
throw InvalidCastException
L_ok:
unbox.any <ExpectedValueType>
```

#### Value type -> interface

```il
box <ActualValueType>
castclass <ExpectedInterfaceType>
```

### Null-aware nullable duck chaining path

`AddIlToDuckChain` has explicit nullable branch.

Representative IL:

```il
dup
brtrue.s L_notNull
pop
ldloca.s nullableLocal
initobj valuetype System.Nullable`1<...>
ldloc.s nullableLocal
br.s L_ret
L_notNull:
call CreateCache<T>.Create(...)
newobj instance void valuetype System.Nullable`1<T>::.ctor(!0)
L_ret:
```

### Dry-run behavior and IL emission

In dry run (`proxyTypeBuilder == null`):
- `LazyILGenerator` accepts calls with null underlying `ILGenerator`.
- Emission methods still execute type/conversion checks.
- No runtime type is created, but failure paths are validated and captured.

This is why dry run catches many errors before real generation.

---

## Exception taxonomy

Major exception classes and triggers.

### Argument and setup

- `DuckTypeProxyTypeDefinitionIsNull`
- `DuckTypeTargetObjectInstanceIsNull`

### Type conversion

- `DuckTypeInvalidTypeConversionException`

### Properties and fields

- `DuckTypePropertyCantBeReadException`
- `DuckTypePropertyCantBeWrittenException`
- `DuckTypePropertyArgumentsLengthException`
- `DuckTypeFieldIsReadonlyException`
- `DuckTypePropertyOrFieldNotFoundException`
- `DuckTypeStructMembersCannotBeChangedException`
- `DuckTypeDuckCopyStructDoesNotContainsAnyField`

### Methods and signatures

- `DuckTypeTargetMethodNotFoundException`
- `DuckTypeProxyMethodParameterIsMissingException`
- `DuckTypeProxyAndTargetMethodParameterSignatureMismatchException`
- `DuckTypeProxyAndTargetMethodReturnTypeMismatchException`
- `DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException`
- `DuckTypeTargetMethodAmbiguousMatchException`

### Reverse proxy constraints

- `DuckTypeReverseProxyBaseIsStructException`
- `DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException`
- `DuckTypeReverseProxyPropertyCannotBeAbstractException`
- `DuckTypeIncorrectReverseMethodUsageException`
- `DuckTypeIncorrectReversePropertyUsageException`
- `DuckTypeReverseProxyMissingPropertyImplementationException`
- `DuckTypeReverseProxyMissingMethodImplementationException`
- `DuckTypeReverseAttributeParameterNamesMismatchException`
- `DuckTypeReverseProxyMustImplementGenericMethodAsGenericException`

### Attribute restrictions

- `DuckTypeCustomAttributeHasNamedArgumentsException`

---

## Analyzer and code fix

Analyzer:
- Diagnostic ID: `DDDUCK001`
- Rule: Avoid null-checking `IDuckType` itself, prefer checking `.Instance` for null.

Rationale:
- Proxy object may be non-null while wrapped target instance can be null in some flows.

Code fix behavior:
- Rewrites checks like `duck == null` to `duck?.Instance == null`.
- Handles binary null checks and `is null` patterns.

Files:
- `DuckTypeNullCheckAnalyzer.cs`
- `DuckTypeNullCheckCodeFixProvider.cs`

---

## Known limitations and known detection gaps

### Hard limitations (by design or current implementation)

1. Reverse base cannot be struct.
2. Reverse implementor cannot be interface/abstract.
3. Generic proxy method with non-public instance dynamic path is unsupported.
4. Named arguments in copied custom attributes are unsupported.
5. Writing members on struct-declared target members is blocked.
6. Readonly fields cannot be set.

### Known validation/detection gaps (test-documented)

Some tests are skipped or annotated due to currently incomplete mismatch detection in specific edge cases, including examples around:
- Certain wrong return-type detection paths in property or field chain scenarios.
- Certain generic wrong-return or conversion mismatch paths.
- Some reverse duck-chain argument mismatch detection paths.

Review current skipped tests under:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/`

Representative files to check first:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Fields/TypeChaining/TypeChainingFieldErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Properties/ReferenceType/ReferenceTypePropertyErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Properties/TypeChaining/TypeChainingPropertyErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/Methods/Generics/GenericMethodErrorTests.cs`
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Errors/ReverseProxy/ReverseProxyErrorTests.cs`

---

## Best practices for contributors

1. Prefer `TryDuckCast` when target shape may vary by package/runtime version.
2. Use `DuckCast` only where cast is expected to be present and failure should be exceptional.
3. Keep proxy definitions minimal. Add only members you consume.
4. Use `[DuckField(Name = "_...")]` for private/backing fields.
5. Add comma-separated fallback names when version drift is known.
6. Use `BindingFlags.IgnoreCase` only when necessary for fragile payloads.
7. Use `ParameterTypeNames` and `GenericParameterTypeNames` to remove ambiguity.
8. Use `ExplicitInterfaceTypeName` for explicit interface implementations.
9. Use `[DuckCopy]` struct proxies for read-only projection style access.
10. In reverse mode, ensure full abstract contract coverage.
11. When checking for null payload, check `IDuckType.Instance`, not only proxy variable.
12. For instrumentation hot paths, avoid additional wrappers if plain shape proxy is enough.

---

## Detailed examples

Examples below are inspired by patterns and tests in `Datadog.Trace.DuckTyping.Tests` and production integration code.

### Example 1: Basic forward interface proxy

```csharp
internal interface IRequestProxy
{
    string Url { get; }
    int StatusCode { get; }
}

object request = GetUnknownRequestObject();
var proxy = request.DuckCast<IRequestProxy>();
Console.WriteLine($"{proxy.Url} -> {proxy.StatusCode}");
```

### Example 2: Field mapping with explicit private backing field

```csharp
internal interface IConnectionProxy
{
    [DuckField(Name = "_connectionString")]
    string ConnectionString { get; }
}
```

### Example 3: Property-or-field fallback

```csharp
internal interface IVersionedProxy
{
    [DuckPropertyOrField(Name = "Data, _data")]
    object Data { get; }
}
```

Behavior:
- Tries property `Data` first.
- Falls back to field `_data`.

### Example 4: Method overload disambiguation by parameter type names

```csharp
internal interface ISerializerProxy
{
    [Duck(Name = "Serialize", ParameterTypeNames = new[]
    {
        "System.String",
        "MyNamespace.Payload, MyAssembly"
    })]
    string SerializePayload(string name, object payload);
}
```

### Example 5: Generic method on non-public target using explicit generic type names

```csharp
internal interface IFactoryProxy
{
    [Duck(Name = "Create", GenericParameterTypeNames = new[]
    {
        "System.Int32",
        "System.String"
    })]
    Tuple<int, string> CreateIntString(int id, string name);
}
```

### Example 6: Explicit interface implementation binding

```csharp
internal interface IExplicitProxy
{
    [Duck(Name = "MoveNext", ExplicitInterfaceTypeName = "System.Collections.IEnumerator")]
    bool MoveNext();
}
```

Wildcard explicit interface matching:

```csharp
internal interface IExplicitWildcardProxy
{
    [Duck(Name = "Current", ExplicitInterfaceTypeName = "*")]
    object Current { get; }
}
```

### Example 7: Multi-name fallback for version drift

```csharp
internal interface ITestMethodRunnerProxy
{
    [DuckField(Name = "_executor,_methodExecutor")]
    object Executor { get; set; }
}
```

### Example 8: `ValueWithType<T>` return wrapper

```csharp
internal interface IResponseProxy
{
    ValueWithType<object> Payload { get; }
}

var payload = response.DuckCast<IResponseProxy>().Payload;
Console.WriteLine($"Runtime type = {payload.Type.FullName}");
```

### Example 9: DuckCopy struct projection

```csharp
[DuckCopy]
internal struct RoutePatternStruct
{
    public string RawText;

    [DuckField(Name = "_defaults")]
    public object Defaults;
}

var route = endpoint.DuckCast<RoutePatternStruct>();
```

### Example 10: Reverse proxy implementing third-party contract

```csharp
internal abstract class ThirdPartyBase
{
    public abstract string GetName();
}

internal class MyImplementation
{
    [DuckReverseMethod(Name = "GetName")]
    public string GetNameReverse() => "datadog";
}

var reverse = new MyImplementation().DuckImplement(typeof(ThirdPartyBase));
var typed = (ThirdPartyBase)reverse;
Console.WriteLine(typed.GetName()); // datadog
```

### Example 11: Reverse property mapping

```csharp
internal abstract class SourceBase
{
    public abstract int Count { get; set; }
}

internal class Impl
{
    [DuckReverseMethod(Name = "Count")]
    public int Count { get; set; }
}
```

### Example 12: Safe probing with `TryDuckCast`

```csharp
if (obj.TryDuckCast<IOptionalFeatureProxy>(out var feature))
{
    Use(feature);
}
```

### Example 13: Null-safe class projection with `DuckAs`

```csharp
var proxy = maybeObj.DuckAs<IMaybeProxy>();
if (proxy is not null)
{
    Use(proxy);
}
```

### Example 14: `IDuckType` null check best practice

```csharp
if (duck?.Instance is null)
{
    // no underlying object
}
```

### Example 15: Method with `ref` and duck chaining

```csharp
internal interface IWithRefProxy
{
    void Transform(ref IInnerProxy value);
}
```

Behavior:
- Proxy argument can be converted into target expected type.
- Return/ref value is converted back after call.

### Example 16: Optional parameter fallback

```csharp
internal interface IOptionalArgsProxy
{
    void Add(string key);
}

// Target has Add(string key, string value = "default")
```

### Example 17: Static field access

```csharp
internal interface IStaticProxy
{
    [DuckField(Name = "_globalCounter")]
    int GlobalCounter { get; set; }
}
```

### Example 18: Forcing class proxy for interface

```csharp
[DuckAsClass]
internal interface IClassProxy
{
    string Name { get; }
}
```

### Example 19: Ignoring a problematic member

```csharp
internal interface IPartialProxy
{
    string Name { get; }

    [DuckIgnore]
    string BrokenMember { get; }
}
```

### Example 20: Including object-level method

```csharp
internal interface IHashProxy
{
    [DuckInclude]
    int GetHashCode();
}
```

### IL companion for Detailed examples (1-20)

This subsection maps each Detailed example to representative emitted IL.

Conventions:
- `TargetType`, `ProxyType`, method tokens, and local indices are illustrative placeholders.
- When multiple runtime branches exist (public/non-public or direct/dynamic), both are shown.

#### Example 1 IL: Basic forward interface proxy (`Url`, `StatusCode`)

Representative getter IL (`get_Url`):

```il
ldarg.0
ldfld     class TargetType ProxyType::_currentInstance
callvirt  instance string TargetType::get_Url()
ret
```

Representative getter IL (`get_StatusCode`):

```il
ldarg.0
ldfld     class TargetType ProxyType::_currentInstance
callvirt  instance int32 TargetType::get_StatusCode()
ret
```

Non-public accessor branch uses `calli` with function pointer.

#### Example 2 IL: Field mapping with `[DuckField(Name = "_connectionString")]`

```il
ldarg.0
ldfld     class TargetType ProxyType::_currentInstance
ldfld     string TargetType::_connectionString
ret
```

If static field mapping is used, `ldsfld` is emitted instead.

#### Example 3 IL: Property-or-field fallback (`Data, _data`)

Binding resolution happens at generation time:
- If property found first:

```il
ldarg.0
ldfld     class TargetType ProxyType::_currentInstance
callvirt  instance object TargetType::get_Data()
ret
```

- Else field path:

```il
ldarg.0
ldfld     class TargetType ProxyType::_currentInstance
ldfld     object TargetType::_data
ret
```

#### Example 4 IL: Method overload disambiguation via `ParameterTypeNames`

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
ldarg.1    // name
ldarg.2    // payload
// conversion for payload as needed
callvirt   instance string TargetType::Serialize(string, class MyNamespace.Payload)
ret
```

If non-public method accessor, call site uses `calli`.

#### Example 5 IL: Generic method binding via `GenericParameterTypeNames`

Closed generic target method is selected at build time (for example `Create<int,string>`):

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
ldarg.1
ldarg.2
callvirt   instance class [System.Runtime]System.Tuple`2<int32,string>
          TargetType::Create<int32,string>(int32, string)
ret
```

#### Example 6 IL: Explicit interface binding (`MoveNext`, wildcard `Current`)

Representative method IL:

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
// target method resolved as explicit interface member
callvirt   instance bool TargetType::System.Collections.IEnumerator.MoveNext()
ret
```

Wildcard case resolves candidate by suffix match and emits equivalent call.

#### Example 7 IL: Multi-name field fallback (`_executor,_methodExecutor`)

Resolved field chosen at generation; getter and setter are direct field ops:

Getter:
```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
ldfld      object TargetType::_executor   // or _methodExecutor
ret
```

Setter:
```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
ldarg.1
stfld      object TargetType::_executor   // or _methodExecutor
ret
```

#### Example 8 IL: `ValueWithType<object>` wrapper on property return

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
callvirt   instance object TargetType::get_Payload()
ldtoken    object
call       class [System.Runtime]System.Type System.Type::GetTypeFromHandle(valuetype System.RuntimeTypeHandle)
call       valuetype Datadog.Trace.DuckTyping.ValueWithType`1<object>
          Datadog.Trace.DuckTyping.ValueWithType`1<object>::Create(object, class System.Type)
ret
```

#### Example 9 IL: DuckCopy struct projection

Struct copy helper pattern (`CreateStructCopyMethod`):

```il
// locals: proxyLocal (generated proxy), structLocal (DuckCopy struct)
ldloca.s   proxyLocal
ldarg.0
castclass  TargetType
call       instance void GeneratedProxy::.ctor(TargetType)

ldloca.s   structLocal
initobj    valuetype RoutePatternStruct

// field copy for each non-ignored writable field in struct definition:
ldloca.s   structLocal
ldloca.s   proxyLocal
call       instance string GeneratedProxy::get_RawText()
stfld      string RoutePatternStruct::RawText

ldloca.s   structLocal
ldloca.s   proxyLocal
call       instance object GeneratedProxy::get_Defaults()
stfld      object RoutePatternStruct::Defaults

ldloc.s    structLocal
ret
```

#### Example 10 IL: Reverse proxy for abstract `GetName()`

Generated override calls delegation implementor method marked `[DuckReverseMethod]`:

```il
ldarg.0
ldfld      class MyImplementation ReverseProxy::_currentInstance
callvirt   instance string MyImplementation::GetNameReverse()
ret
```

#### Example 11 IL: Reverse property mapping (`Count`)

Getter:
```il
ldarg.0
ldfld      class Impl ReverseProxy::_currentInstance
callvirt   instance int32 Impl::get_Count()
ret
```

Setter:
```il
ldarg.0
ldfld      class Impl ReverseProxy::_currentInstance
ldarg.1
callvirt   instance void Impl::set_Count(int32)
ret
```

#### Example 12 IL: `TryDuckCast` probing path

`TryDuckCast` itself is C# logic, then proxy activator call when creatable.

Representative hot success path:

```il
// pseudo-flow
if (instance is T) return true;
result = DuckType.CreateCache<T>.GetProxy(instance.GetType());
if (!result.Success) return false;
value = result.CreateInstance<T>(instance);   // invokes cached delegate
return true;
```

Activator delegate body (representative):

```il
ldarg.0
castclass  TargetType
newobj     instance void GeneratedProxy::.ctor(TargetType)
ret
```

#### Example 13 IL: `DuckAs<T>` null-safe projection

Representative flow:

```il
if (instance is null) return null;
if (instance is T) return (T)instance;
result = DuckType.CreateCache<T>.GetProxy(instance.GetType());
if (!result.Success) return null;
return result.CreateInstance<T>(instance);
```

Proxy instance creation IL is identical to forward activator path.

#### Example 14 IL: `duck?.Instance is null` consumer check

This is consumer-side IL, not generated by DuckTyping emitters.

Representative shape:

```il
ldloc      duck
brfalse.s  L_null
ldloc      duck
callvirt   instance object Datadog.Trace.DuckTyping.IDuckType::get_Instance()
brfalse.s  L_null
// non-null branch
```

Analyzer `DDDUCK001` exists to encourage this pattern over raw duck variable null checks.

#### Example 15 IL: `ref` parameter bridge with duck chaining

Representative bridge shape (forward):

```il
// prepare local for target-compatible ref
ldarg.<refArg>
ldind.ref
castclass  Datadog.Trace.DuckTyping.IDuckType
callvirt   instance object Datadog.Trace.DuckTyping.IDuckType::get_Instance()
// conversion to target ref element type
stloc.s    localRef
ldloca.s   localRef

// invoke target
callvirt   instance void TargetType::Transform(class TargetInner&)

// write back to proxy ref arg
ldarg.<refArg>
ldloc.s    localRef
call       !0 Datadog.Trace.DuckTyping.DuckType/CreateCache`1<class IInnerProxy>::Create(object)
stind.ref
ret
```

#### Example 16 IL: Optional parameter fallback

When selected target signature has optional trailing parameters:

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
ldarg.1
callvirt   instance void TargetType::Add(string) // or Add(string, optional=default) depending selected signature
ret
```

Method selection logic validates optional compatibility before emit.

#### Example 17 IL: Static field access

Getter:
```il
ldsfld     int32 TargetType::_globalCounter
ret
```

Setter:
```il
ldarg.1
stsfld     int32 TargetType::_globalCounter
ret
```

#### Example 18 IL: `[DuckAsClass]` class proxy shape

Constructor and getter pattern:

```il
// .ctor(TargetType instance)
ldarg.0
ldarg.1
stfld      class TargetType GeneratedClassProxy::_currentInstance
ret

// get_Name
ldarg.0
ldfld      class TargetType GeneratedClassProxy::_currentInstance
callvirt   instance string TargetType::get_Name()
ret
```

#### Example 19 IL: `[DuckIgnore]` member omission

Only non-ignored members are emitted.

If `BrokenMember` is ignored, there is no generated `get_BrokenMember`/`set_BrokenMember` method in emitted proxy type.

Representative emitted method set:

```il
// get_Name exists
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
callvirt   instance string TargetType::get_Name()
ret

// get_BrokenMember is not emitted
```

#### Example 20 IL: `[DuckInclude]` object-level method inclusion (`GetHashCode`)

Without `[DuckInclude]`, object-level methods are skipped in selection.
With `[DuckInclude]`, generated method exists:

```il
ldarg.0
ldfld      class TargetType ProxyType::_currentInstance
callvirt   instance int32 [System.Runtime]System.Object::GetHashCode()
ret
```

For non-public override path, call site may switch to `calli`.

---

## Feature-by-feature test-adapted excerpts

This section adds concrete snippets directly adapted from tests.

Goal:
- Make each major feature family visible with real code patterns.
- Keep snippets short but representative.
- Show which feature IDs in the catalog the snippet exercises.

Note:
- Snippets are adapted for readability.
- Referenced test files contain the full validation scenarios and assertions.

### Excerpt A: Interface proxy defaults to value type, `[DuckAsClass]` forces class

Features:
- 1, 18 (proxy shape)

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckAsClassTests.cs`

```csharp
public class TargetObject
{
    public string SayHi() => "Hello World";
}

public interface IDefaultInterfaceProxy
{
    string SayHi();
}

[DuckAsClass]
public interface IClassInterfaceProxy
{
    string SayHi();
}

var target = new TargetObject();

var defaultProxy = target.DuckCast<IDefaultInterfaceProxy>();
var classProxy = target.DuckCast<IClassInterfaceProxy>();

// default interface proxy is emitted as value type
Assert.True(defaultProxy.GetType().IsValueType);

// DuckAsClass interface proxy is emitted as class
Assert.True(classProxy.GetType().IsClass);
```

### Excerpt B: Property-or-field fallback prefers property, then field

Features:
- 7

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/PropertyOrFieldTests.cs`

```csharp
[DuckCopy]
public struct Proxy
{
    [DuckPropertyOrField]
    public int Value;
}

public class TargetWithProperty
{
    public int Value { get; } = 1;
}

public class TargetWithField
{
    public int Value = 1;
}

var p1 = new TargetWithProperty().DuckCast<Proxy>();
var p2 = new TargetWithField().DuckCast<Proxy>();
```

### Excerpt C: Name fallback list (`\"a,b\"`) for fields, properties, reverse properties

Features:
- 8, 38, 41

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/MultipleNamesTests.cs`

```csharp
private interface IFieldObjects
{
    [DuckField(Name = "_fieldName1,_fieldName2")]
    string FieldName { get; set; }
}

private interface IPropertyObjects
{
    [Duck(Name = "PropertyName1,PropertyName2")]
    string PropertyName { get; set; }
}

public class ReverseProxyWithProperties
{
    [DuckReverseMethod(Name = "Value1,Value2")]
    public string Value { get; set; } = "Datadog";
}
```

### Excerpt D: Explicit interface binding including wildcard mode

Features:
- 25

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckExplicitInterfaceTests.cs`

```csharp
public interface IProxyDefinition
{
    [Duck(ExplicitInterfaceTypeName = "Datadog.Trace.DuckTyping.Tests.DuckExplicitInterfaceTests+ITarget")]
    string SayHi();

    [Duck(ExplicitInterfaceTypeName = "*")]
    string SayHiWithWildcard();
}
```

### Excerpt E: Include object-level method only when explicitly requested

Features:
- 12, 13

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckIncludeTests.cs`

```csharp
public class SomeClassWithDuckInclude
{
    [DuckInclude]
    public override int GetHashCode() => 42;
}

public interface IInterface { }

var proxy = new SomeClassWithDuckInclude().DuckCast<IInterface>();
// proxy.GetHashCode() routes to target GetHashCode only when [DuckInclude] is present
```

### Excerpt F: Ignore members in proxy definitions and rely on local/default implementation

Features:
- 11

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckIgnoreTests.cs`

```csharp
public abstract class AbstractPrivateProxy : IGetValue
{
    public abstract ValuesDuckType Value { get; }

    [DuckIgnore]
    public string GetValueProp => Value.ToString();

    [DuckIgnore]
    public string GetValue() => Value.ToString();

    public abstract int GetAnswerToMeaningOfLife();
}
```

### Excerpt G: Reverse proxy implementation with parameter type disambiguation

Features:
- 27, 38, 39

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ReverseProxyTests.cs`

```csharp
public class PublicLogEventEnricherImpl
{
    [DuckReverseMethod(ParameterTypeNames = new[]
    {
        "Serilog.Events.LogEvent, Serilog",
        "Serilog.Core.ILogEventPropertyFactory, Serilog"
    })]
    public void Enrich(ILogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Implementation delegated by generated reverse proxy
    }
}

Type iLogEventEnricherType = typeof(Serilog.Core.ILogEventEnricher);
var reverse = new PublicLogEventEnricherImpl().DuckImplement(iLogEventEnricherType);
```

### Excerpt H: Reverse proxy custom-attribute copy behavior

Features:
- 41

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ReverseProxyTests.cs`

```csharp
[MyCustomAttribute("datadog")]
public class ReverseProxyWithAttributeAndArgumentsTestClass
{
    [DuckReverseMethod(Name = "Value")]
    public string Property { get; set; }
}

var proxy = (IReverseProxyWithPropertiesTest)new ReverseProxyWithAttributeAndArgumentsTestClass()
    .DuckImplement(typeof(IReverseProxyWithPropertiesTest));

// Generated reverse proxy type carries MyCustomAttribute("datadog")
```

Named-argument attributes are rejected in reverse proxy generation and throw `DuckTypeCustomAttributeHasNamedArgumentsException`.

### Excerpt I: `DuckCopy` + nullable duck chaining

Features:
- 31, 34, 36

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckChainingNullableTests.cs`

```csharp
[DuckCopy]
public struct SFirstClass
{
    public SSecondClass? Value;
}

[DuckCopy]
public struct SSecondClass
{
    public string Name;
}

public class FirstClass
{
    public SecondClass Value { get; set; }
}

var original = new FirstClass();
var copy1 = original.DuckCast<SFirstClass>();   // Value = null

original.Value = new SecondClass { Name = "Hello World" };
var copy2 = original.DuckCast<SFirstClass>();   // Value now populated
```

### Excerpt J: `ValueWithType<T>` return wrappers

Features:
- 32

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Methods/MethodTests.cs`

```csharp
public interface IObscureDuckType
{
    [Duck(Name = "Sum")]
    ValueWithType<int> SumReturnValueWithType(int a, int b);
}

var proxy = obscure.DuckCast<IObscureDuckType>();
var result = proxy.SumReturnValueWithType(10, 10);

Assert.Equal(20, result.Value);
Assert.Equal(typeof(int), result.Type);
```

### Excerpt K: ref/out signatures and object-vs-duck bridges

Features:
- 20, 29

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Methods/MethodTests.cs`

```csharp
public interface IObscureDuckType
{
    void Pow2(ref int value);

    [Duck(Name = "GetOutput")]
    void GetOutputObject(out object value);

    bool TryGetObscure(out IDummyFieldObject obj);

    [Duck(Name = "TryGetObscure")]
    bool TryGetObscureObject(out object obj);
}

int value = 4;
proxy.Pow2(ref value); // value updated through ref bridge

proxy.GetOutputObject(out object boxedOut);
proxy.TryGetObscure(out IDummyFieldObject duckOut);
proxy.TryGetObscureObject(out object rawOut);
```

### Excerpt L: overload disambiguation with `ParameterTypeNames`

Features:
- 18, 21

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Methods/ProxiesDefinitions/IObscureDuckType.cs`

```csharp
public interface IObscureDuckType
{
    [Duck(ParameterTypeNames = new[]
    {
        "System.String",
        "Datadog.Trace.DuckTyping.Tests.ObscureObject+DummyFieldObject, Datadog.Trace.DuckTyping.Tests"
    })]
    void Add(string name, object obj);

    void Add(string name, int obj);
    void Add(string name, string obj = "none");
}
```

### Excerpt M: field visibility mapping with `[DuckField]`

Features:
- 5, 10

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Fields/ValueType/ProxiesDefinitions/IObscureDuckType.cs`

```csharp
public interface IObscureDuckType
{
    [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
    int PublicStaticReadonlyValueTypeField { get; }

    [DuckField(Name = "_privateValueTypeField")]
    int PrivateValueTypeField { get; set; }

    [DuckField(Name = "_publicNullableIntField")]
    int? PublicNullableIntField { get; set; }
}
```

### Excerpt N: property mapping, indexers, and `ValueWithType<T>` for properties

Features:
- 1, 2, 4, 32

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Properties/ReferenceType/ProxiesDefinitions/IObscureDuckType.cs`

```csharp
public interface IObscureDuckType
{
    string PublicGetSetReferenceType { get; set; }

    [Duck(Name = "PublicStaticGetSetReferenceType")]
    ValueWithType<string> PublicStaticOnlyGetWithType { get; }

    string this[string index] { get; set; }
}
```

### Excerpt O: null-semantics differences across API entry points

Features:
- public surface null behavior

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ExceptionsTests.cs`

```csharp
public interface IMyProxy { }

// Core API throws on null instance
Assert.Throws<DuckTypeTargetObjectInstanceIsNull>(() =>
{
    DuckType.Create(typeof(IMyProxy), null);
});

// Extension APIs are null-safe
DuckTypeExtensions.TryDuckCast(null, out IMyProxy value).Should().BeFalse();
DuckTypeExtensions.DuckAs<IMyProxy>(null).Should().BeNull();
DuckTypeExtensions.DuckIs<IMyProxy>(null).Should().BeFalse();
DuckTypeExtensions.TryDuckImplement(null, typeof(IMyProxy), out var reverse).Should().BeFalse();
```

### Excerpt P: long generated type-name safety path

Features:
- dynamic proxy naming and truncation behavior

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/LongTypeNameTests.cs`

```csharp
public interface ILongNamedTypeProxy
{
    string Value { get; }
}

[DuckCopy]
public struct LongNamedTypeProxy
{
    public string Value;
}

// Test verifies duck typing still works when target FullName exceeds 1024 chars.
var proxyA = veryLongNamedInstance.DuckCast<ILongNamedTypeProxy>();
var proxyB = veryLongNamedInstance.DuckCast<LongNamedTypeProxy>();
```

### Excerpt Q: deep generics referencing private types across assemblies

Features:
- 10 (visibility handling), private type compatibility in generic graphs

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/PrivateGenericTests.cs`

```csharp
public interface IDuckType
{
    IDuckTypeInner Method { get; }
}

public interface IDuckTypeInner
{
    string Value { get; }
}

var instance = new TargetObject<IEnumerable<Tuple<BoundedConcurrentQueue<MockHttpParser>, Span>>>();
var duckType = instance.DuckCast<IDuckType>();
Assert.NotNull(duckType.Method);
Assert.False(string.IsNullOrWhiteSpace(duckType.Method.Value));
```

### Excerpt R: `ToString()` passthrough across proxy kinds

Features:
- 14

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/DuckToStringByPassTests.cs`

```csharp
public class TargetClass
{
    public override string ToString() => "ToString from Target instance.";
}

public interface IEmptyProxy { }
public interface IToStringProxy { string ToString(); }
public abstract class EmptyAbstractProxyClass { }

var instance = new TargetClass();
Assert.Equal(instance.ToString(), instance.DuckCast<IEmptyProxy>().ToString());
Assert.Equal(instance.ToString(), instance.DuckCast<IToStringProxy>().ToString());
Assert.Equal(instance.ToString(), instance.DuckCast<EmptyAbstractProxyClass>().ToString());
```

### Excerpt S: reverse duck chaining in property assignment

Features:
- 30, 42

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/ReverseProxyTests.cs`

```csharp
public class TestValue { }

public interface IReverseProxyWithDuckChainingPropertiesTest
{
    TestValue Value { get; set; }
}

public class ReverseProxyWithDuckChainingPropertiesTestClass
{
    [DuckReverseMethod(Name = "Value")]
    public object Value { get; set; }
}

var instance = new ReverseProxyWithDuckChainingPropertiesTestClass();
var proxy = (IReverseProxyWithDuckChainingPropertiesTest)instance.DuckImplement(typeof(IReverseProxyWithDuckChainingPropertiesTest));

var input = new TestValue();
proxy.Value = input;

Assert.IsAssignableFrom<IDuckType>(instance.Value);
Assert.Same(input, ((IDuckType)instance.Value).Instance);
```

### Excerpt T: default optional argument fallback

Features:
- 19

Inspired by:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/Methods/MethodTests.cs`

```csharp
public interface IOptionalArgsProxy
{
    void Add(string name, string obj = "none");
}

// Target may define optional/default semantics with compatible trailing arguments.
var proxy = obscure.DuckCast<IOptionalArgsProxy>();
proxy.Add("KeyString01");
```

---

## Source map

Core files:
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Methods.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Properties.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Fields.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Utilities.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckType.Statics.cs`

Helpers:
- `tracer/src/Datadog.Trace/DuckTyping/ILHelpersExtensions.cs`
- `tracer/src/Datadog.Trace/DuckTyping/LazyILGenerator.cs`
- `tracer/src/Datadog.Trace/DuckTyping/TypesTuple.cs`
- `tracer/src/Datadog.Trace/DuckTyping/ValueWithType.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckTypeConstants.cs`

Contracts and attributes:
- `tracer/src/Datadog.Trace/DuckTyping/IDuckType.cs`
- `tracer/src/Datadog.Trace/DuckTyping/IDuckTypeTask.cs`
- `tracer/src/Datadog.Trace/DuckTyping/IDuckTypeAwaiter.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAttributeBase.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckFieldAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckPropertyOrFieldAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckCopyAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckAsClassAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckReverseMethodAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckIncludeAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckIgnoreAttribute.cs`
- `tracer/src/Datadog.Trace/DuckTyping/DuckTypeAttribute.cs`

Errors:
- `tracer/src/Datadog.Trace/DuckTyping/DuckTypeExceptions.cs`

Tests:
- `tracer/test/Datadog.Trace.DuckTyping.Tests/`

Analyzer:
- `tracer/src/Datadog.Trace.Tools.Analyzers/DuckTypeAnalyzer/DuckTypeNullCheckAnalyzer.cs`
- `tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/DuckTypeAnalyzer/DuckTypeNullCheckCodeFixProvider.cs`

---

## Final note

This document is intentionally exhaustive and optimized for maintainers and instrumentation authors.

If behavior and this doc diverge, implementation and tests are authoritative.
