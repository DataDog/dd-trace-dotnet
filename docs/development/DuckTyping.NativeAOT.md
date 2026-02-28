# DuckTyping NativeAOT Guide

## Purpose

This document describes how to use the DuckTyping NativeAOT pipeline end-to-end:

1. Author proxy definitions.
2. Generate an AOT registry assembly at build time.
3. Wire the generated registry into a NativeAOT application.
4. Validate compatibility and parity.
5. Run official DuckTyping AOT sample and parity workflows.

This guide is implementation-accurate for the current branch state (February 28, 2026).

## Scope and Non-Goals

This guide covers the NativeAOT DuckTyping path only. Dynamic (runtime IL emit) DuckTyping is documented in [DuckTyping.md](./DuckTyping.md) and [DuckTyping.Bible.md](./DuckTyping.Bible.md).

Key design rule for NativeAOT:

1. No runtime proxy IL emission.
2. Proxies are generated into a separate assembly at build time.
3. Runtime only resolves and instantiates pre-registered proxies.

## Architecture Overview

NativeAOT DuckTyping has two phases.

1. Build-time phase (`ducktype-aot generate`):
   1. Discover mappings from proxy attributes and/or map file.
   2. Resolve proxy and target types from provided assemblies.
   3. Emit a registry assembly containing generated proxy types and bootstrap code.
   4. Emit companion artifacts (`manifest`, `compat`, linker descriptor, props).
2. Runtime phase:
   1. Bootstrap initializes AOT mode and validates contract.
   2. Bootstrap registers forward/reverse mappings.
   3. `DuckType.GetOrCreateProxyType` and `DuckType.GetOrCreateReverseProxyType` resolve from AOT registry.

### Runtime Isolation Rules

DuckType runtime mode is immutable per process.

1. First mode wins (`dynamic` or `aot`).
2. Switching mode later throws `DuckTypeRuntimeModeConflictException`.
3. AOT runtime allows a single generated registry assembly identity per process.

## NativeAOT Runtime APIs

NativeAOT bootstrapping uses these public APIs on `DuckType`:

1. `DuckType.EnableAotMode()`
2. `DuckType.ValidateAotRegistryContract(...)`
3. `DuckType.RegisterAotProxy(...)`
4. `DuckType.RegisterAotReverseProxy(...)`

The generated bootstrap calls all of these automatically.

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

### Reverse Proxy

Reverse mappings are supported, but are declared through map/discovery entries with `mode = "reverse"`.

```json
{
  "mode": "reverse",
  "proxyType": "MyNamespace.IReverseContract",
  "proxyAssembly": "My.Proxy.Assembly",
  "targetType": "MyNamespace.ReverseDelegation",
  "targetAssembly": "My.Target.Assembly"
}
```

## Mapping Sources

`ducktype-aot generate` composes mappings from:

1. Proxy assembly attribute discovery (`[DuckType]`, `[DuckCopy]`).
2. Optional map file (`--map-file`) for additions/overrides/exclusions.
3. Optional mapping catalog (`--mapping-catalog`) for required coverage enforcement.

If the same mapping key appears multiple times, later resolution overwrites previous entries. `excludes`/`exclude=true` removes keys.

## Map File Schema

`--map-file` accepts JSON with `mappings` and optional `excludes`.

```json
{
  "mappings": [
    {
      "scenarioId": "MY-01",
      "mode": "forward",
      "proxyType": "My.Namespace.IProxy",
      "proxyAssembly": "My.Proxy.Assembly",
      "targetType": "My.Namespace.TargetType",
      "targetAssembly": "My.Target.Assembly"
    },
    {
      "scenarioId": "MY-02",
      "mode": "reverse",
      "proxyType": "My.Namespace.IReverseProxy",
      "proxyAssembly": "My.Proxy.Assembly",
      "targetType": "My.Namespace.ReverseDelegation",
      "targetAssembly": "My.Target.Assembly"
    }
  ],
  "excludes": [
    {
      "mode": "forward",
      "proxyType": "My.Namespace.IOldProxy",
      "proxyAssembly": "My.Proxy.Assembly",
      "targetType": "My.Namespace.OldTarget",
      "targetAssembly": "My.Target.Assembly"
    }
  ]
}
```

Notes:

1. `mode` defaults to `forward`.
2. Valid modes: `forward`, `reverse`.
3. `proxyType` and `targetType` can be assembly-qualified; if so, assembly can be inferred.

## Generic Instantiation Roots

Use `--generic-instantiations` to preserve additional closed generic roots.

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

## Running the Generator

The command is currently hidden from root help, but callable directly.

Runner assembly path (Release build):

`tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll`

### Minimal Command

```bash
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-assembly /abs/path/My.Target.Assembly.dll \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.dll
```

### Full Command (recommended)

```bash
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-assembly /abs/path/My.Target.Assembly.dll \
  --target-folder /abs/path/extra-targets \
  --target-filter "*.dll" \
  --map-file /abs/path/ducktype-aot-map.json \
  --mapping-catalog /abs/path/ducktype-aot-catalog.json \
  --require-mapping-catalog \
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
2. Neither `--target-assembly` nor `--target-folder` is provided.
3. No `--target-filter` is provided.
4. File paths do not exist.
5. Open generic mappings remain unresolved.
6. Required mapping catalog entries are missing (when enabled).

## Generated Artifacts

Given output `X.dll`, generator emits:

1. `X.dll`:
   1. generated proxy types.
   2. bootstrap type `Datadog.Trace.DuckTyping.Generated.DuckTypeAotRegistryBootstrap`.
   3. module initializer that calls bootstrap `Initialize()`.
2. `X.dll.manifest.json`: build metadata, assembly fingerprints, mapping snapshot.
3. `X.dll.compat.json`: machine-readable compatibility matrix per mapping.
4. `X.dll.compat.md`: human-readable compatibility report.
5. Linker descriptor:
   1. default `X.dll.linker.xml`, or `--emit-trimmer-descriptor` path.
6. Props file:
   1. default `X.dll.props`, or `--emit-props` path.
   2. adds registry reference + `TrimmerRootDescriptor`.

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
3. Call bootstrap explicitly at startup.

```csharp
using Datadog.Trace.DuckTyping.Generated;

DuckTypeAotRegistryBootstrap.Initialize();
```

Explicit `Initialize()` is recommended to make startup behavior unambiguous, even though module initializer exists.

## Hands-On Quickstart: Create and Run an AOT DuckTyping App

This section is a full from-scratch example you can copy/paste.

### Prerequisites

1. Repository root is available as `REPO_ROOT`.
2. .NET 8 SDK is installed.
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

dotnet new classlib -n SampleDuckContracts -f net8.0
dotnet new console -n SampleDuckApp -f net8.0
```

### 3. Add Contracts/Targets/Proxies

Create `SampleDuckContracts/ValueContracts.cs`:

```csharp
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
RUNNER_DLL="$REPO_ROOT/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll"
REGISTRY_DLL="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.dll"
REGISTRY_PROPS="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.props"
REGISTRY_LINKER="$WORK_DIR/Datadog.Trace.DuckType.AotRegistry.Sample.linker.xml"

dotnet "$RUNNER_DLL" ducktype-aot generate \
  --proxy-assembly "$CONTRACTS_DLL" \
  --target-assembly "$CONTRACTS_DLL" \
  --map-file "$WORK_DIR/ducktype-aot-map.json" \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.Sample \
  --emit-props "$REGISTRY_PROPS" \
  --emit-trimmer-descriptor "$REGISTRY_LINKER" \
  --output "$REGISTRY_DLL"
```

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

`$REPO_ROOT/tracer/src/Datadog.Trace/bin/Release/net6.0/Datadog.Trace.dll`

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
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/SampleDuckContracts.dll \
  --target-assembly /abs/path/SampleDuckContracts.dll \
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
  dotnet test tracer/test/Datadog.Trace.Tools.Runner.Tests/Datadog.Trace.Tools.Runner.Tests.csproj \
  -c Release --framework net8.0 \
  --filter FullyQualifiedName~DuckTypeAotFullSuiteParityIntegrationTests
```

### Direct full-suite AOT run (manual)

If you already have a generated registry assembly for the suite, run tests directly in AOT mode:

```bash
DD_DUCKTYPE_TEST_MODE=aot \
DD_DUCKTYPE_AOT_REGISTRY_PATH=/abs/path/Datadog.Trace.DuckType.AotRegistry.dll \
  dotnet test tracer/test/Datadog.Trace.DuckTyping.Tests/Datadog.Trace.DuckTyping.Tests.csproj \
  -c Release --framework net8.0 --no-build
```

### AOT processor + NativeAOT integration test suite

```bash
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
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot generate \
  --proxy-assembly /abs/path/My.Proxy.Assembly.dll \
  --target-assembly /abs/path/My.Target.Assembly.dll \
  --map-file /abs/path/discovered-ducktype-aot-map.json \
  --output /abs/path/Datadog.Trace.DuckType.AotRegistry.dll
```

Note: discovery may include runtime-generated dynamic assembly identities in some workloads. Sanitize those entries before generation if needed.

## Compatibility Verification Command

Use `verify-compat` as a contract gate in CI/release.

```bash
dotnet tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net8.0/Datadog.Trace.Tools.Runner.dll \
  ducktype-aot verify-compat \
  --compat-report /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.md \
  --compat-matrix /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.compat.json \
  --mapping-catalog tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-mapping-catalog.json \
  --scenario-inventory tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-scenario-inventory.json \
  --expected-outcomes tracer/test/Datadog.Trace.DuckTyping.Tests/AotCompatibility/ducktype-aot-bible-expected-outcomes.json \
  --manifest /abs/path/Datadog.Trace.DuckType.AotRegistry.dll.manifest.json \
  --failure-mode strict
```

### Verify-compat Inputs

1. `--compat-report` and `--compat-matrix` are required.
2. Optional contracts:
   1. `--mapping-catalog`
   2. `--scenario-inventory`
   3. `--expected-outcomes`
   4. `--manifest`
3. `--known-limitations` is legacy alias for expected outcomes.
4. `--failure-mode` values:
   1. `default`: manifest fingerprint drift warns.
   2. `strict`: manifest fingerprint drift fails.

## Compatibility Status and Diagnostics

Current status values include:

1. `compatible`
2. `pending_proxy_emission`
3. `unsupported_proxy_kind`
4. `missing_proxy_type`
5. `missing_target_type`
6. `missing_target_method`
7. `non_public_target_method`
8. `incompatible_method_signature`
9. `unsupported_proxy_constructor`
10. `unsupported_closed_generic_mapping`

Current diagnostic codes emitted by generator:

1. `DTAOT0202` `unsupported_proxy_kind`
2. `DTAOT0204` `missing_proxy_type`
3. `DTAOT0205` `missing_target_type`
4. `DTAOT0207` `missing_target_method`
5. `DTAOT0209` `incompatible_method_signature`
6. `DTAOT0210` `unsupported_proxy_constructor`
7. `DTAOT0211` `unsupported_closed_generic_mapping`

## Environment Variables Summary

### Generation and Build

1. `DD_TRACE_DUCKTYPE_AOT_STRONG_NAME_KEY_FILE`
   1. Optional strong-name key path for generated registry signing.

### Discovery and Test Harness

1. `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH`
   1. Test/migration workflow output path for dynamic discovery map entries.
2. `DD_DUCKTYPE_TEST_MODE`
   1. `dynamic` or `aot` for test runtime bootstrap.
3. `DD_DUCKTYPE_AOT_REGISTRY_PATH`
   1. Test runtime path to generated registry assembly.
4. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY`
   1. Enables full suite parity integration orchestration test.

## Troubleshooting

### `AOT duck typing mapping not found`

Cause:

1. Requested proxy-target pair is not registered in AOT registry.

Actions:

1. Confirm mapping exists in map/attribute discovery and output `.compat.json`.
2. Confirm bootstrap `Initialize()` runs before first DuckType call.
3. Confirm correct registry assembly is loaded.

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

Actions:

1. Regenerate registry with the same Datadog.Trace build used at runtime.
2. Re-check manifest metadata.

### `unsupported_closed_generic_mapping`

Cause:

1. Closed generic mapping requires unsupported adaptation pattern.

Actions:

1. Prefer direct assignable closed generic pair.
2. Refactor mapping/proxy shape.
3. Add compatible closed generic roots where needed.

### Missing assembly resolution during generation

Cause:

1. Mapped assembly name not present in provided target/proxy inputs.

Actions:

1. Add missing `--proxy-assembly` or `--target-assembly`.
2. Add `--target-folder` + `--target-filter` for assembly closure.

## Recommended CI Gates

Use all of these gates together.

1. Dynamic baseline suite green:
   1. `DD_DUCKTYPE_TEST_MODE=dynamic ... Datadog.Trace.DuckTyping.Tests`
2. Full parity orchestration gate:
   1. `DD_RUN_DUCKTYPE_AOT_FULL_SUITE_PARITY=1 ... DuckTypeAotFullSuiteParityIntegrationTests`
3. AOT processor and NativeAOT integration:
   1. `... --filter FullyQualifiedName~DuckTypeAot`
4. Optional explicit verify-compat step against generated artifacts.

## Cross-Reference

1. Dynamic DuckTyping guide: [DuckTyping.md](./DuckTyping.md)
2. Complete DuckTyping behavior and examples: [DuckTyping.Bible.md](./DuckTyping.Bible.md)
3. NativeAOT parity stabilization plan: [DuckTyping-NativeAOT-Parity-Stabilization-Plan.md](./for-ai/DuckTyping-NativeAOT-Parity-Stabilization-Plan.md)
