# Datadog.Trace.Aot NuGet package

> Preview: NativeAOT support is being developed.

This package instruments .NET applications published with [NativeAOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
(`PublishAot=true`) with the Datadog tracer. NativeAOT applications can't be instrumented at runtime (there is no JIT for a
profiler to rewrite methods, and no dynamic code), so the instrumentation happens at build time, when the application is
published.

## Usage

Reference the package and publish with NativeAOT:

```xml
<PackageReference Include="Datadog.Trace.Aot" Version="..." />
```

```shell
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
```

Only `dotnet publish` with `PublishAot=true` is affected: `dotnet build` and `dotnet run` are unchanged.

## How does it work

After the NativeAOT compiler (ILC) computes its inputs, `dd-trace aot instrument` rewrites the application and the
assemblies it compiles with it (the framework and the packages) with the Datadog native tracer hosted offline, like the
profiler does at runtime. It also generates the code the instrumentation needs instead of dynamic code: the CallTarget
registrations and a DuckType AOT registry. The rewritten assemblies replace ILC's inputs.

## Properties

| Property | Description |
|---|---|
| `DatadogAotInstrumentation` | `false` to disable the instrumentation. |
| `DatadogAotDuckTypeMaps` | ducktype-aot map files recorded at runtime with `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH` (`;`-separated): the duck typing mappings created from runtime types. |
| `DatadogAotCategories` | Instrumentation categories (default `tracing,appsec,rasp`; add `iast` to opt in). |
| `DatadogAotFailOnError` | `true` to fail the publish when the instrumentation fails. By default, the application is published without it, with a warning. |
