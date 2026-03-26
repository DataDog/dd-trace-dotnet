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
2. A target type.
3. A proxy interface.
4. A generated DuckTyping AOT registry assembly.
5. A startup call to `DuckTypeAotRegistryBootstrap.Initialize()`.

## Minimal App Layout

```text
NativeAotDuckSample/
├─ NativeAotDuckSample.csproj
└─ Program.cs
```

## Minimal Source Code

### `Program.cs`

```csharp
using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.DuckTyping.Generated;

public class Person
{
    public string Name { get; set; } = string.Empty;
}

public interface IPersonProxy
{
    string Name { get; }
}

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

### `NativeAotDuckSample.csproj`

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
  </ItemGroup>

  <Import Project="$(DuckTypeAotPropsPath)"
          Condition="'$(DuckTypeAotPropsPath)' != '' and Exists('$(DuckTypeAotPropsPath)')" />
</Project>
```

Notes:

1. The `<Import />` is how the generated registry assembly and linker metadata are brought into publish.
2. `DuckTypeAotPropsPath` is passed on the publish command line.
3. This sample uses a repo `ProjectReference` because it is meant for local development in this repository.

## Build the Runner Tool First

The generator lives in `Datadog.Trace.Tools.Runner`.

```bash
dotnet build /Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj -c Release --no-restore
```

Runner path:

```text
/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll
```

## Build the Sample Once

Build the sample first so the app assembly exists for discovery.

```bash
dotnet build /abs/path/to/NativeAotDuckSample/NativeAotDuckSample.csproj -c Release
```

Assume the sample output is:

```text
/abs/path/to/NativeAotDuckSample/bin/Release/net10.0
```

## Generate the AOT Registry

For the smallest setup, do discovery and generation in one step.

This creates:

1. The generated registry assembly.
2. The generated `.props`.
3. The manifest.
4. Compatibility artifacts.
5. The trimmer descriptor.
6. The canonical discovered map file.

```bash
dotnet /Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll ducktype-aot generate \
  --discover-mappings \
  --proxy-assembly /abs/path/to/NativeAotDuckSample/bin/Release/net10.0/NativeAotDuckSample.dll \
  --target-folder /abs/path/to/NativeAotDuckSample/bin/Release/net10.0 \
  --target-filter NativeAotDuckSample.dll \
  --map-file /abs/path/to/NativeAotDuckSample/artifacts/ducktype-aot-map.json \
  --assembly-name Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample \
  --emit-trimmer-descriptor /abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.linker.xml \
  --emit-props /abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.props \
  --output /abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll
```

This one command:

1. discovers mappings from the proxy definitions found in the app assembly,
2. writes the discovered canonical map to `--map-file`,
3. generates the AOT registry assembly and companion artifacts.

## Optional: Discover the Mapping Separately

Use a separate discovery step only if you want to inspect or check in the canonical map before generation.

```bash
dotnet /Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll ducktype-aot discover-mappings \
  --proxy-assembly /abs/path/to/NativeAotDuckSample/bin/Release/net10.0/NativeAotDuckSample.dll \
  --target-folder /abs/path/to/NativeAotDuckSample/bin/Release/net10.0 \
  --target-filter NativeAotDuckSample.dll \
  --output /abs/path/to/NativeAotDuckSample/artifacts/ducktype-aot-map.json
```

## Publish the NativeAOT App

Pass the generated `.props` into publish.

```bash
dotnet publish /abs/path/to/NativeAotDuckSample/NativeAotDuckSample.csproj \
  -c Release \
  -r osx-arm64 \
  /p:DuckTypeAotPropsPath=/abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.props
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
dotnet /Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net10.0/Datadog.Trace.Tools.Runner.dll ducktype-aot verify-compat \
  --compat-matrix /abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll.compat.json \
  --map-file /abs/path/to/NativeAotDuckSample/artifacts/ducktype-aot-map.json \
  --manifest /abs/path/to/NativeAotDuckSample/artifacts/Datadog.Trace.DuckType.AotRegistry.NativeAotDuckSample.dll.manifest.json
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
