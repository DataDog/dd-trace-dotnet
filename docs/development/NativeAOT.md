# NativeAOT instrumentation

How the tracer instruments applications published with NativeAOT (`PublishAot=true`). Customer-facing documentation is
the README of the [`Datadog.Trace.Aot`](../../tracer/src/Datadog.Trace.Aot/README.md) package; duck typing has its own
documents ([`DuckTyping.NativeAOT.md`](./DuckTyping.NativeAOT.md) and the related ones).

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
(`DatadogAotFailOnError=true` fails the publish).

The runner starts with `DOTNET_ROLL_FORWARD=LatestMajor` (it may be the only runtime), then `AotRuntimeSelector` runs it
again with `dotnet exec --fx-version` on the latest runtime of the application's major version: the DuckType generator
loads the application's assemblies, which an older runtime can't load and a newer one replaces with its own versions
(.NET 11 has `Microsoft.Extensions.*.Abstractions` in the shared framework).

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

### Duck typing without dynamic code

Proxies are generated by the DuckType AOT registry generator (see [`DuckTyping.NativeAOT.md`](./DuckTyping.NativeAOT.md))
for:

- the duck typing constraints of the bound integrations (`DuckProxyRequestCollector`);
- the mappings created from runtime types, which nothing names statically: the package's catalog
  (`tracer/src/Datadog.Trace.Aot/ducktype-maps/datadog-trace.json`, recorded under JIT with
  `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH`) and the application's maps (`DatadogAotDuckTypeMaps`).

The generator evaluates dynamic duck typing with the application's types in its own process, so the registry behaves like
it (including its failures). The runtime of the generator's framework stands for the application's framework assemblies
(the NativeAOT `System.Private.CoreLib` is never the generator's).

### What else ILC needs

- `Datadog.Trace.Json.*`: the JSON models Newtonsoft reads by reflection (`JsonModelDescriptor`);
- `Datadog.Trace.Reflection.*`: the library types integrations find by name (`ReflectionRootDescriptor`);
- `Datadog.Trace.Delegates.rd.xml`: the delegate wrappers of the delegate instrumentation (`DelegateWrapperDirectives`);
- `Datadog.Trace.Facades.rd.xml`: `mscorlib` when Hangfire resolves type names with it (`FacadeDirectives`);
- `Datadog.Trace.DuckType.Composites.dll`: the composite interfaces of reverse proxies (`CompositeInterfaceAssembly`);
- for the application's module initializer: the IAST hardcoded secrets (user strings), the Code Origin locations of
  endpoints and the SourceLink of the PDB, which the tracer reads from files at runtime under JIT.

## Runtime

The application's module initializer registers the DuckType AOT registry (duck typing then runs in AOT mode) and the
build-time data, then calls `Instrumentation.InitializeAot()` (`Instrumentation.IsBuildTimeInstrumented`), which
initializes the tracer like `Instrumentation.Initialize` without the native tracer (`NativeMethods.CanBeLoaded` is false
without dynamic code) and enables the instrumentation categories. Each step is in its own `try`/`catch`: a failure is
logged and the application goes on. Products that instrument at runtime (Dynamic Instrumentation, Exception Replay,
Symbol Database) and the Continuous Profiler aren't available. The native libraries the tracer P/Invokes (`LibDatadog`,
`libddwaf`) come with the package's runtime assets.

NativeAOT specifics in the tracer:

- `StackFrame.GetMethod()` is null for methods without reflection metadata: `StackFrameMethod` reads the names from the
  stack trace data (`DiagnosticMethodInfo` in .NET 9+, the frame's text in .NET 8) for RASP and IAST;
- file and line in stack frames need .NET 11 and `StackTraceLineNumberSupport` (the package enables it).

## Testing

| What | Where |
|---|---|
| Runner units (registry generation, binder, emulation, descriptors, runtime selection...) | `tracer/test/Datadog.Trace.Tools.Runner.Tests` (`Aot*`, `CallTargetRegistryGeneratorTests`, `DuckTypeAot*`) |
| CallTarget holders and continuations | `tracer/test/Datadog.Trace.Tests/CallTarget/CallTargetAotHolderTests.cs` |
| Native tracer hosted offline on `CallTargetNativeTest` (same output as the profiler, determinism, build environment) | `AotInstrumentNativeHostIntegrationTests` (`DD_AOT_NATIVE_TRACER`, `DD_AOT_CALLTARGET_NATIVE_TEST_DIR`) |
| NativeAOT publish of an ASP.NET Core application | `AotInstrumentNativeAotPublishIntegrationTests.AspNetCoreSpansReachTheAgent` |
| The package: manual API, AppSec, RASP, IAST, AOT analysis warnings of `Datadog.Trace` (Verify snapshot), binary size budget | `AotInstrumentNativeAotPublishIntegrationTests.ManualApiApplicationWithThePackage` (`DD_AOT_PACKAGE_FEED`) |

CI runs the native host and ASP.NET Core tests with `RunNativeAotInstrumentationGate` (Nuke) in a unit test job of each
build host: Linux x64 and arm64, macOS and Windows. Integration samples are compared between JIT and NativeAOT in
[`NativeAOT.Samples.md`](./NativeAOT.Samples.md).

To try it locally: build the tracer (`./tracer/build.sh BuildTracerHome`), pack the package with
`./tracer/build.sh BuildNativeAotNuget --MonitoringHome <home with every platform>`, or point an application to the
targets and the tools directly (`DatadogAotRunnerPath`, `DatadogAotNativeTracerPath`, `DatadogAotDatadogTracePath`).
