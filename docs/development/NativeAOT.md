# NativeAOT instrumentation

How the tracer instruments applications published with NativeAOT (`PublishAot=true`). Customer-facing documentation is
the README of the [`Datadog.Trace.Aot`](../../tracer/src/Datadog.Trace.Aot/README.md) package; duck typing has its own
documents ([`DuckTyping.NativeAOT.md`](./DuckTyping.NativeAOT.md) and the related ones). Bugs found and not fixed:
[`NativeAOT.KnownIssues.md`](./NativeAOT.KnownIssues.md).

## Why build time

Under JIT, the native tracer (a CLR profiler) rewrites methods with ReJIT when they are compiled, and the managed tracer
creates code at runtime (`DynamicMethod` for the CallTarget adapters, `Reflection.Emit` for duck typing proxies). A
NativeAOT application has neither: no profiler API, no JIT, no dynamic code. So everything is done when the application is
published, before the NativeAOT compiler (ILC) compiles it:

1. the IL the native tracer would produce at runtime is produced offline and given to ILC instead of the original
   assemblies;
2. the code the managed tracer would create at runtime is generated as IL too.

## Build time

```
dotnet publish -p:PublishAot=true
  └─ Datadog.Trace.Aot.targets (after ComputeIlcCompileInputs, before WriteIlcRspFileForCompilation)
       └─ dd-trace aot instrument @arguments.rsp           (own process: the native tracer has process-wide state)
            ├─ AotRuntimeSelector: run again on the runtime of the application's major version
            ├─ NativeTracerHost: Datadog.Tracer.Native loaded in-process, driven through ICorProfilerCallback
            │    └─ EmulatedRuntime: ICorProfilerInfo8/12 and IMetaData* over dnlib modules
            ├─ CallTargetRegistryGenerator: adapters + registrations for every rewritten method
            ├─ DuckType AOT registry: proxies for the duck typing constraints and the recorded mappings
            ├─ descriptors for ILC: JSON models, types found by name, delegate wrappers, facades, composites
            └─ module initializer of the application → Instrumentation.InitializeAot()
       └─ IlcCompileInput / IlcReference replaced by the instrumented assemblies (obj/.../datadog-aot/)
```

### MSBuild

[`Datadog.Trace.Aot.targets`](../../tracer/src/Datadog.Trace.Aot/build/Datadog.Trace.Aot.targets) only acts on a
NativeAOT publish (`dotnet build` and `dotnet run` are unchanged), never overwrites `obj/` outputs of the build, and is
incremental (inputs: ILC's inputs, the maps, the tools). It passes the application first (its module initializer starts
the tracer), then every `IlcReference` but `System.Private.CoreLib` and `Datadog.Trace`. When the application only
references the manual API (the `Datadog.Trace` NuGet package), the package's full `Datadog.Trace.dll` is added to ILC's
inputs. On failure, the application is published without the instrumentation, with a warning
(`DatadogAotFailOnError=true` fails the publish, also when the tools aren't found). Each instrumentation deletes the
outputs of the previous one first: an assembly it doesn't rewrite anymore isn't compiled in its previous version.

The runner starts with `DOTNET_ROLL_FORWARD=LatestMajor` (it may be the only runtime), then `AotRuntimeSelector` runs it
again with `dotnet exec --fx-version` on the latest runtime of the application's major version: the DuckType generator
loads the application's assemblies, which an older runtime can't load and a newer one replaces with its own versions
(.NET 11 has `Microsoft.Extensions.*.Abstractions` in the shared framework). The package ships the tool build of the runner:
the trimmed standalone `dd-trace` doesn't have the `aot` and `ducktype-aot` commands, which load and reflect over the
assemblies they process.

### The native tracer hosted offline

[`NativeTracerHost`](../../tracer/src/Datadog.Trace.Tools.Runner/Aot/Native/NativeTracerHost.cs) loads the same
`Datadog.Tracer.Native` library the profiler uses (the package has one per build host: win-x64/x86, linux x64/arm64 glibc
and musl, macOS universal) and calls it like the CLR does: `Initialize`, `ModuleLoadFinished` for every assembly, the JIT
events, then the ReJIT callbacks of the methods it asked to rewrite. The native tracer's environment only has the settings
of the offline mode: the build environment's `DD_*` variables are the application's runtime settings, not the build's,
and the native logs go to `obj/.../datadog-aot/logs`.

[`EmulatedRuntime`](../../tracer/src/Datadog.Trace.Tools.Runner/Aot/Native/EmulatedRuntime.cs) implements the subset of
`ICorProfilerInfo` the native tracer uses (it answers `ICorProfilerInfo12`, required on ARM, but not `ICorProfilerInfo10`,
which would enable the runtime ReJIT path), and
[`ModuleMetadata`](../../tracer/src/Datadog.Trace.Tools.Runner/Aot/Native/ModuleMetadata.cs) the `IMetaDataImport2` /
`IMetaDataEmit2` / `IMetaDataAssemblyImport` / `IMetaDataAssemblyEmit` calls over dnlib modules, with the CLR's token and
row semantics. The rewritten bodies are written back with dnlib (`MethodBodies`), with their sequence points transferred
(`SequencePointTransfer`) so the PDBs ILC reads still map them. Three native exports exist for this host only:
`WaitForPendingRejitWork` (a barrier instead of timing), `ProcessCallSites` (the IAST/RASP call site rewriting the JIT
events trigger at runtime) and `InitializeTraceMethods` (`DD_TRACE_METHODS` at publish).

### CallTarget without dynamic code

At runtime, `BeginMethodHandler`, `EndMethodHandler` and the continuation generators build their delegates with
`IntegrationMapper` (`DynamicMethod`). [`CallTargetRegistryGenerator`](../../tracer/src/Datadog.Trace.Tools.Runner/Aot/CallTarget/CallTargetRegistryGenerator.cs)
scans the rewritten methods for their `CallTargetInvoker` calls and generates, in the instrumented assembly, an adapter
per handler (the same IL `IntegrationMapper` emits, `IntegrationBinder` mirrors its binding rules and failures) and a
registration that stores it in `CallTargetAot<TIntegration, TDelegate>` with the runtime checks `IntegrationMapper` does.
The handlers read those holders first. Open generic targets bind through their generic context, and the closed
instantiations `GenericInstantiationDiscovery` finds in the instrumented assemblies get registrations of their own (e.g.
the continuation of a `T M<T>()` called with `Task<int>`). Every integration is registered with its instrumentation
categories (`CallTargetAotCategories`), which the tracer enables like the native definitions at runtime (tracing, AppSec,
RASP; IAST only when published with it).

The search of instantiations only follows the generic code that leads to an instrumented generic method: a first pass over
the method bodies finds the generic types and methods that reference one with their own generic parameters (directly or
not, and the generic virtual methods their implementations override), and the references with closed type arguments to
them, in any method, start the search. Generic cycles (`M<T>` calling `M<List<T>>`) are cut at a nesting depth of 8. When
the search is cut (a cycle, or its limit of contexts) and instrumented generic methods need instantiations (a duck typing
proxy of their generic target, the continuation of a generic return type), the build shows the warning `DDAOT001`, and
fails with `DatadogAotFailOnError`: those integrations wouldn't trace the instantiations that are missing (an incomplete
registration falls back to `IntegrationMapper`, which can't bind without dynamic code). The report lists the methods
(`GenericInstantiations`), also those the application doesn't instantiate (e.g. ASP.NET Core Identity in an application
without it).

### Duck typing without dynamic code

Proxies are generated by the DuckType AOT registry generator (see [`DuckTyping.NativeAOT.md`](./DuckTyping.NativeAOT.md))
for:

- the duck typing constraints of the bound integrations (`DuckProxyRequestCollector`). The begin of a method with more than 8
  arguments (`object[]`) looks the proxies of its arguments up by runtime type (`IntegrationMapper.ConvertType`): the registry
  has the proxy of each type assignable to the static type of the argument, except for a proxy that binds no member (a marker
  interface), which the proxy of `System.Object` serves for every type (an `object` argument would otherwise get a proxy per
  type of the application, too many for the NativeAOT compiler);
- the mappings created from runtime types, which nothing names statically: the ones Datadog's assemblies declare with
  attributes (see below) and the application's maps (`DatadogAotDuckTypeMaps`, recorded under JIT with
  `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH`).

The mappings created from runtime types are declared next to their proxies, and only generated when the application
references the target's assembly:

| Declaration | Mapping |
|---|---|
| `[DuckType("Target.Type", "TargetAssembly")]` on a proxy interface or class | proxy over the target |
| `[DuckType(..., IncludeDerivedTypes = true)]` | one proxy over a base type, used for the types deriving from it (the generator can't know them: e.g. the responses of AWS SDK clients) |
| ``[DuckType("Library.Type`2", "Library")]`` (an open generic type) | a proxy generic over the type parameters of the target, instantiated at runtime for the instantiations the library creates over the application's types (e.g. gRPC's call handlers); reference type arguments only |
| `[DuckCopy("Target.Type", "TargetAssembly")]` on a `[DuckCopy]` struct | copy of the target |
| `[DuckReverseDelegation("Base.Type", "BaseAssembly")]` on the Datadog type a reverse proxy delegates to | reverse proxy deriving from the base type |
| `[assembly: DuckTypeMapping(proxy, proxyAssembly, target, targetAssembly)]` | proxies of another Datadog assembly or closed generic proxies |

When the publish has recorded maps, the report lists in `UndeclaredDuckTypeMappings` the recorded mappings of Datadog's
proxies that aren't declared, so a mapping an integration creates under JIT and its declarations miss shows up there.

CI checks the declarations against the integration tests: on Linux, for .NET 6+ (NativeAOT applications use the net6.0
build of Datadog.Trace, and the builds of older runtimes have code paths of their own), `RunIntegrationTests` sets
`DD_TEST_DUCKTYPE_AOT_MAPS_DIRECTORY`, so each sample the tests start records its mappings (`DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH`)
into a map of `artifacts/ducktype-aot-maps` naming its directory (`applicationDirectory`). Then `CheckAotDuckTypeDeclarations`
runs `dd-trace aot check-declarations`, which fails when a recorded mapping of a Datadog proxy isn't declared (the report goes
to `build_data/ducktype-aot-declarations.json`). The targets are resolved from the directory of the sample, for the derived
types. Left out: the mappings of the duck typing constraints of CallTarget integrations, which the build generates from the
instrumented methods (the recorder marks them `"callTarget": true`), the ones dynamic duck typing doesn't create, which fail
without a declaration too (`"canCreate": false`), and the proxies of CI Visibility, Dynamic Instrumentation and Exception
Replay (their NativeAOT support is planned separately: the NativeAOT modes of the test frameworks run the tests with other
assemblies, such as `xunit.v3.core.aot`, or are previews, such as `MSTest.Engine`).

The generator evaluates dynamic duck typing with the application's types in its own process, so the registry behaves like
it (including its failures). The runtime of the generator's framework stands for the application's framework assemblies
(the NativeAOT `System.Private.CoreLib` is never the generator's).

### What else ILC needs

- `Datadog.Trace.Json.*`: the JSON models Newtonsoft reads by reflection (`JsonModelDescriptor`);
- `Datadog.Trace.Reflection.*`: the library types integrations find by name (`ReflectionRootDescriptor`);
- `Datadog.Trace.Delegates.rd.xml`: the delegate wrappers of the delegate instrumentation (`DelegateWrapperDirectives`);
- `Datadog.Trace.Facades.rd.xml`: `mscorlib` when Hangfire resolves type names with it (`FacadeDirectives`);
- `Datadog.Trace.Libraries.linker.xml`: what instrumented libraries use by reflection and can't run without, for the
  libraries of the application: the librdkafka bindings of Confluent.Kafka (`LibraryDescriptors`);
- `Datadog.Trace.DuckType.Composites.dll`: the composite interfaces of reverse proxies (`CompositeInterfaceAssembly`);
- for the application's module initializer: the IAST hardcoded secrets (user strings), the Code Origin locations of
  endpoints and the SourceLink of the PDB, which the tracer reads from files at runtime under JIT.

Without the ASP.NET Core assemblies (a console or worker application), the code of Datadog.Trace that uses them can't run:
the targets set the feature switch `Datadog.Trace.AspNetCore.IsSupported` to false (`AspNetCoreAvailabilityChecker`), and
ILC leaves that code out instead of compiling it to throw.

What doesn't depend on the application (the JSON models, the delegate wrappers, the type names Datadog.Trace looks up and
its duck typing declarations) is computed once per build of Datadog.Trace: `dd-trace aot prepare` writes it to
`Datadog.Trace.aot.json` next to `Datadog.Trace.dll` (`DatadogTraceAnalysis`), which the package ships. The
instrumentation uses it when it's the analysis of the same builds of Datadog.Trace and of the runner, else analyzes
Datadog.Trace again (several seconds of each publish). The runner and the application use the same build of Datadog.Trace
(the package ships one): the generator checks the application's duck typing with dynamic duck typing on the runner's.

### Trimming and AOT warnings

A NativeAOT publish of an application with Datadog shows no trimming or AOT warning of Datadog.Trace, also with
`TrimmerSingleWarn=false` (which lists the warnings `IL2104`/`IL3053` sum up), with ILC 8 and 10. Each warning gets one of:

- the code that needs dynamic code is left out: checks of `RuntimeFeature.IsDynamicCodeSupported` (a constant for ILC) keep
  out the dynamic duck typing engine (`DuckTypeAotEngine.IsDynamicCodeSupported`), the binding of CallTarget integrations
  at runtime (`IntegrationMapper.IsSupported`), Dynamic Instrumentation, Exception Replay and the Symbol Database
  (`DebuggerManager.RuntimeDebuggerProductsSupported`: they instrument methods or read the assemblies' files at runtime, and
  log a warning when they're enabled) and Service Fabric Remoting (it emits its proxies). ILC 9+ propagates the constant
  through those properties; for ILC 8, `ILLink.Substitutions.xml` stubs them when the feature switch is false;
- an alternative that works there: `Marshal.GetDelegateForFunctionPointer<T>` for the WAF, `typeof(Func<T>)` instead of
  `MakeGenericType` in the delegate wrappers, the `long` instantiations of the .NET 8 runtime metrics polyfill
  (`Datadog.Trace.RuntimeMetrics.rd.xml`, `RuntimeMetricsDirectives`);
- a suppression that says why the reflection works: the build keeps what it needs (JSON models, delegate wrappers, the
  types looked up by name, composite interfaces), or the code isn't used by Datadog.Trace. The vendored libraries' are in
  `VendorsTrimming.cs` (their files are generated);
- Newtonsoft.Json's support of dynamic objects (C# `dynamic` needs dynamic code): substitutions that only apply without
  dynamic code replace the creation of its binder and call sites, which kept Microsoft.CSharp and the call sites of
  System.Linq.Expressions (ILC 8 warns in them), and the descriptor of the types looked up by name leaves the binder out.
  The JIT builds keep every feature.

CI checks it with the package's application, published for .NET 8 and .NET 10 (`RunNativeAotPackageTests`, the
`native_aot_package_tests` job once the package is built): its Verify snapshots of the warnings of Datadog.Trace are empty.

## Runtime

The application's module initializer registers the DuckType AOT registry (duck typing then runs in AOT mode) and the
build-time data, then calls `Instrumentation.InitializeAot()` (`Instrumentation.IsBuildTimeInstrumented`), which
initializes the tracer like `Instrumentation.Initialize` without the native tracer (`NativeMethods.CanBeLoaded` is false
without dynamic code) and enables the instrumentation categories. Each step is in its own `try`/`catch`: a failure is
logged and the application goes on. Products that instrument at runtime (Dynamic Instrumentation, Exception Replay,
Symbol Database), Test Optimization (CI Visibility) and the Continuous Profiler aren't available. ILC leaves their code
out of the application: `DebuggerManager.RuntimeDebuggerProductsSupported` is a constant without dynamic code, the targets
set the `Datadog.Trace.TestOptimization.IsSupported` feature switch to false (`TestOptimizationAvailability`), and the
instrumentation doesn't preserve their JSON models. The native libraries the tracer P/Invokes (`LibDatadog`, `libddwaf`)
come with the package's runtime assets.

NativeAOT specifics in the tracer:

- `StackFrame.GetMethod()` is null for methods without reflection metadata: `StackFrameMethod` reads the names from the
  stack trace data (`DiagnosticMethodInfo` in .NET 9+, the frame's text in .NET 8) for RASP and IAST;
- file and line in stack frames need .NET 11 and `StackTraceLineNumberSupport` (the package enables it).

## Not supported yet

### OpenTelemetry thread context (OTEP 4947)

`DD_TRACE_OTEL_CTX_ENABLED` (opt-in, Linux) publishes the active span of each thread for external readers, such as the
OpenTelemetry eBPF profiler (see [OTelContextPropagation.md](../OTelContextPropagation.md)). Readers look up the ELF TLS
symbol `otel_thread_ctx_v1` in the dynamic symbol table of the process's modules. Under JIT, `Datadog.Tracer.Native.so`
defines it (`otel_thread_ctx.cpp`) along with `GetOrCreateOtelThreadContextRecord`. That function rents each thread's
record and returns it to a pool when the thread exits (a `pthread_key` destructor). The managed tracer owns the record
layout and every write (`OtelThreadContextRecord`). A NativeAOT application doesn't load the native tracer:
`OtelThreadContextPublisher.Create` logs that instrumentation isn't attached and returns the null publisher.

The native part can't move to C#:

- NativeAOT can't define an ELF TLS symbol or export data. A `[ThreadStatic]` lives in the runtime's TLS block, which has
  no symbol of its own, and `UnmanagedCallersOnly(EntryPoint = ...)` exports functions only.
- The record goes back to the pool from a `pthread_key` destructor while the thread exits. NativeAOT detaches the thread
  from the runtime in its own thread-exit callback, in no order relative to that destructor, so managed code can't run
  there (a reverse P/Invoke would attach the thread again).

A NativeAOT application is already a native executable, so the solution is to link that code into it, with nothing to
deploy next to the application:

1. **Static library.** Build `otel_thread_ctx.cpp`, unchanged, as a static library for `linux-x64` and `linux-arm64`
   (e.g. `libDatadog.OtelThreadContext.a`), with `-fPIC` (NativeAOT executables are position independent) and the same
   TLS dialect probe as the shared target. The JIT build keeps it in the shared target. The file only needs pthreads and
   `posix_memalign`, resolved when the application is linked, so one archive per architecture should serve glibc and musl:
   check it on Alpine.
2. **Package.** `Datadog.Trace.Aot` ships the archives outside `runtimes/` (NuGet copies `runtimes/<RID>/native` next to
   the published application), e.g. `build/native/<RID>/`, collected by `BuildNativeAotNuget` from the tracer home.
3. **Targets.** For Linux RIDs, `Datadog.Trace.Aot.targets` links the archive and binds the one P/Invoke to it directly
   (the other `Datadog.Tracer.Native` P/Invokes stay unresolved, as now):

   ```xml
   <NativeLibrary Include="$(MSBuildThisFileDirectory)native/$(RuntimeIdentifier)/libDatadog.OtelThreadContext.a" />
   <DirectPInvoke Include="Datadog.Tracer.Native!GetOrCreateOtelThreadContextRecord" />
   <LinkerArg Include="-Wl,--dynamic-list=$(MSBuildThisFileDirectory)native/otel_thread_ctx.list" />
   <RuntimeHostConfigurationOption Include="Datadog.Trace.OtelThreadContext.IsLinked" Value="true" Trim="true" />
   ```

   An executable doesn't export its symbols, whatever their visibility: the linker has to add `otel_thread_ctx_v1` to the
   dynamic symbol table. The OTEP advises `--export-dynamic-symbol` for executables, which needs GNU ld 2.35 or later;
   `--dynamic-list` (a file with `{ otel_thread_ctx_v1; };`) does the same with older linkers. A static archive member is
   only linked when something references it: the direct P/Invoke does.
4. **Tracer.** `OtelThreadContextPublisher.Create` also accepts a NativeAOT application whose build linked the library
   (the `Datadog.Trace.OtelThreadContext.IsLinked` switch) instead of requiring the profiler. ILC treats the switch as a
   constant, so the applications without the library don't keep that code.

In an executable, the linker relaxes the TLS access to local-exec, which the OTEP requires readers to handle. The slot
is then in the executable's static TLS block, without the static TLS surplus limit of a library loaded with `dlopen`.

To validate:

- `readelf --dyn-syms -W <application> | grep otel_thread_ctx_v1` shows an 8-byte `TLS GLOBAL DEFAULT` symbol, and
  `readelf -r` the access model the linker produced;
- `OtelThreadContextTests` with the sample published with NativeAOT. The sample resolves the slot's address with
  `dlsym` on `Datadog.Tracer.Native.so`; in NativeAOT, it uses the executable's own handle (`dlopen(NULL)`). The test
  then reads the record from another process and compares it with the spans;
- the process context announcement (`OtelProcessContextAnnouncer`, through libdatadog, which the package ships);
- a real reader, such as the [`ctx-sharing-demo` reader](https://github.com/scottgerring/ctx-sharing-demo/tree/main/context-reader)
  or the OpenTelemetry eBPF profiler, on x64 and arm64, glibc and musl.

## Testing

| What | Where |
|---|---|
| Runner units (registry generation, binder, emulation, descriptors, runtime selection...) | `tracer/test/Datadog.Trace.Tools.Runner.Tests` (`Aot*`, `CallTargetRegistryGeneratorTests`, `DuckTypeAot*`) |
| CallTarget holders and continuations | `tracer/test/Datadog.Trace.Tests/CallTarget/CallTargetAotHolderTests.cs` |
| Native tracer hosted offline on `CallTargetNativeTest` (same output as the profiler, determinism, build environment) | `AotInstrumentNativeHostIntegrationTests` (`DD_AOT_NATIVE_TRACER`, `DD_AOT_CALLTARGET_NATIVE_TEST_DIR`) |
| `CallTargetNativeTest` published with NativeAOT (its test integrations instrumented by the targets): the same CallTarget calls as with the profiler, in every mode | `AotInstrumentNativeHostIntegrationTests.CallTargetNativeTestBehavesAsWithTheProfilerInNativeAot` (also `DD_AOT_CALLTARGET_NATIVE_TEST_PROJECT`, `DD_RUN_CALLTARGET_AOT_NATIVEAOT_PUBLISH=1`) |
| NativeAOT publish of an ASP.NET Core application | `AotInstrumentNativeAotPublishIntegrationTests.AspNetCoreSpansReachTheAgent` |
| The package (.NET 8 and .NET 10): manual API, AppSec, RASP, IAST, AOT analysis warnings of `Datadog.Trace` (Verify snapshots), binary size budget | `AotInstrumentNativeAotPublishIntegrationTests.ManualApiApplicationWithThePackage` (`DD_AOT_PACKAGE_FEED`; CI: `RunNativeAotPackageTests`) |
| Parity with JIT (.NET 8 and .NET 10): the same application (ASP.NET Core, HttpClient, manual API, AppSec, RASP, IAST, SQLite with Dapper.AOT, an unhandled exception) published with NativeAOT and the package, and published for JIT and run with the profiler and the tracer home, sends the same spans: operations, resources, errors, parents, tags (the values of the ones that don't depend on the run) and AppSec and IAST events | `AotInstrumentNativeAotPublishIntegrationTests.NativeAotSpansMatchTheJitOnes` (`DD_AOT_PACKAGE_FEED`, `DD_AOT_MONITORING_HOME`; CI: `RunNativeAotPackageTests`) |
| Execution time (timeit) of the execution benchmark samples (`Samples.HttpMessageHandler`, `Samples.FakeDbCommand`) published with NativeAOT: the baseline (without the tracer) and `CallTarget+NativeAOT` (instrumented at build time), next to the JIT ones in the execution benchmarks report | `tracer/build/timeit/Samples.*/*.nativeaot.json`, `PublishNativeAotExecutionBenchmarkSamples` (Nuke; CI: `execution_benchmarks`, published on a hosted Windows agent and run on the benchmark agents) |
| Duck typing declarations (attributes, derived types, generic proxies) and their check against recorded maps | `AotDuckTypeDeclarationsTests`, `DuckTypeAotAdditionalParityTests` (generic proxies, fallbacks), `CheckAotDuckTypeDeclarations` (Nuke, Linux integration tests) |

CI runs the native host and ASP.NET Core tests with `RunNativeAotInstrumentationGate` (Nuke) in a unit test job of each
build host: Linux x64 and arm64, macOS and Windows. Integration samples are compared between JIT and NativeAOT in
[`NativeAOT.Samples.md`](./NativeAOT.Samples.md), and what each integration supports is in
[`NativeAOT.Integrations.md`](./NativeAOT.Integrations.md).

To try it locally: build the tracer (`./tracer/build.sh BuildTracerHome`), pack the package with
`./tracer/build.sh BuildNativeAotNuget --MonitoringHome <home with every platform>`, or point an application to the
targets and the tools directly (`DatadogAotRunnerPath`, `DatadogAotNativeTracerPath`, `DatadogAotDatadogTracePath`).
