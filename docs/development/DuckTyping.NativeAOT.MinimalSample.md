# DuckTyping NativeAOT Minimal Sample

## Purpose

This document shows the smallest practical setup for a NativeAOT application that uses DuckTyping with the generated AOT registry.

Use this when you want:

1. One proxy shape.
2. One target type.
3. One generated registry.
4. One NativeAOT publish command.

This is intentionally smaller than the full guide in [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md).

## What You Build

You need:

1. A NativeAOT app project.
2. A small contracts project containing the target type and proxy interface.
3. A generated DuckTyping AOT registry assembly.
4. A startup call to `DuckTypeAotRegistryBootstrap.Initialize()`.

## Minimal App Layout

```text
NativeAotDuckSample/
├─ NativeAotDuckContracts/
│  ├─ NativeAotDuckContracts.csproj
│  └─ PersonContracts.cs
├─ NativeAotDuckApp/
│  ├─ NativeAotDuckApp.csproj
│  └─ Program.cs
└─ ducktype-aot-map.json
```

## Minimal Source Code

### `NativeAotDuckContracts/PersonContracts.cs`

```csharp
public class Person
{
    public string Name { get; set; } = string.Empty;
}

public interface IPersonProxy
{
    string Name { get; }
}
```

### `NativeAotDuckApp/Program.cs`

```csharp
using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.DuckTyping.Generated;

public static class Program
{
    public static void Main()
    {
        DuckTypeAotRegistryBootstrap.Initialize();

        var person = new Person { Name = "Alice" };
        var proxy = person.DuckCast<IPersonProxy>();

        Console.WriteLine(proxy.Name);
    }
}
```

### `NativeAotDuckContracts/NativeAotDuckContracts.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### `NativeAotDuckApp/NativeAotDuckApp.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="/abs/path/to/dd-trace-dotnet/tracer/src/Datadog.Trace/Datadog.Trace.csproj" />
    <ProjectReference Include="../NativeAotDuckContracts/NativeAotDuckContracts.csproj" />
  </ItemGroup>

  <Import Project="$(DuckTypeAotPropsPath)"
          Condition="'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')" />
</Project>
```

Notes:

1. The `<Import />` is how the generated registry assembly and linker metadata are brought into publish.
2. `DuckTypeAotPropsPath` is passed on the publish command line.
3. The app can reference `Datadog.Trace.DuckTyping.Generated` only after the generated registry props are imported during publish.
4. This sample uses a repo `ProjectReference` because it is meant for local development in this repository.

## Build the Runner Tool First

The generator lives in `Datadog.Trace.Tools.Runner`.

```bash
export REPO_ROOT=/abs/path/to/dd-trace-dotnet
export SAMPLE_ROOT=/abs/path/to/NativeAotDuckSample

dotnet build "$REPO_ROOT/tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj" -c Release --no-restore
```

Runner path:

```text
$REPO_ROOT/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll
```

## Build the Contract Assembly Once

Build the contracts first so the proxy and target assembly exists for generation.

```bash
dotnet build "$SAMPLE_ROOT/NativeAotDuckContracts/NativeAotDuckContracts.csproj" -c Release
```

Assume the contracts output is:

```text
$SAMPLE_ROOT/NativeAotDuckContracts/bin/Release/net10.0
```

## Create the Mapping File

The minimal proxy does not carry discovery metadata, so use an explicit map file:

```json
{
  "schemaVersion": "1",
  "mappings": [
    {
      "mode": "forward",
      "proxyType": "IPersonProxy",
      "proxyAssembly": "NativeAotDuckContracts",
      "targetType": "Person",
      "targetAssembly": "NativeAotDuckContracts"
    }
  ]
}
```

Save this as `$SAMPLE_ROOT/ducktype-aot-map.json`.

## Generate the AOT Registry

This creates:

1. The generated registry assembly.
2. The generated `.props`.
3. The manifest.
4. Compatibility artifacts.
5. The trimmer descriptor.

```bash
export RUNNER="$REPO_ROOT/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll"
mkdir -p "$SAMPLE_ROOT/artifacts"

dotnet "$RUNNER" ducktype-aot generate \
  --proxy-assembly "$SAMPLE_ROOT/NativeAotDuckContracts/bin/Release/net10.0/NativeAotDuckContracts.dll" \
  --target-folder "$SAMPLE_ROOT/NativeAotDuckContracts/bin/Release/net10.0" \
  --target-filter NativeAotDuckContracts.dll \
  --map-file "$SAMPLE_ROOT/ducktype-aot-map.json" \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample \
  --emit-trimmer-descriptor "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.linker.xml" \
  --emit-props "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.props" \
  --output "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll"
```

The command reads the canonical map and generates the AOT registry assembly and companion artifacts.

## Publish the NativeAOT App

Pass the generated `.props` into publish.

```bash
dotnet publish "$SAMPLE_ROOT/NativeAotDuckApp/NativeAotDuckApp.csproj" \
  -c Release \
  -r osx-arm64 \
  /p:DuckTypeAotPropsPath="$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.props"
```

Replace `osx-arm64` with your RID when needed.

## What Happens at Runtime

At startup:

1. `DuckTypeAotRegistryBootstrap.Initialize()` enables AOT mode.
2. It validates the generated registry against the `Datadog.Trace` runtime contract.
3. It registers all generated mappings.
4. `person.DuckCast<IPersonProxy>()` uses the pre-registered AOT mapping.

There is no runtime IL emit fallback in this path.

## Minimal Validation

You can validate the generated compatibility contract before publish:

```bash
dotnet "$RUNNER" ducktype-aot verify-compat \
  --compat-report "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll.compat.md" \
  --compat-matrix "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll.compat.json" \
  --map-file "$SAMPLE_ROOT/ducktype-aot-map.json" \
  --manifest "$SAMPLE_ROOT/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll.manifest.json" \
  --failure-mode strict
```

## Important Constraints

1. The AOT runtime is closed-world. If you add new compatible target types later, regenerate the registry.
2. Runtime lookups are exact-match only. Flexibility comes from generation-time registration expansion.
3. Generated bootstrap is the supported model. Manual AOT registration APIs exist for compatibility, but they are not the preferred integration path.

## Next Docs

After the minimal sample:

1. Read [DuckTyping.NativeAOT.md](./DuckTyping.NativeAOT.md) for the full workflow.
2. Read [DuckTyping.NativeAOT.BuildIntegration.md](./DuckTyping.NativeAOT.BuildIntegration.md) for CI and build wiring.
3. Read [DuckTyping.NativeAOT.Spec.md](./DuckTyping.NativeAOT.Spec.md) for artifact contracts.
