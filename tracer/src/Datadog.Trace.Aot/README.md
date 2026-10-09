# Datadog.Trace.Aot NuGet package

> Preview: NativeAOT support is in preview (prerelease package versions).

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

The published application is configured as usual, with the `DD_*` environment variables (`DD_SERVICE`, `DD_ENV`,
`DD_AGENT_HOST`, `DD_APPSEC_ENABLED`...). It doesn't need the profiler variables (`CORECLR_ENABLE_PROFILING`,
`CORECLR_PROFILER_PATH`...) nor the tracer home: the publish output has everything (the native libraries the tracer uses,
`LibDatadog` and `libddwaf`, are copied next to the executable).

## Requirements

- Applications targeting .NET 8, 9 or 10 (and .NET 11 previews), published with `PublishAot=true`.
- The instrumentation loads the application's assemblies: publish on a machine with the .NET runtime of the version the
  application targets (the SDK of that version brings it). Otherwise it uses the closest newer runtime installed and warns,
  as some duck typing mappings may then be missing.
- A supported build host (see [Platforms](#platforms)).

## How does it work

After the NativeAOT compiler (ILC) computes its inputs, `dd-trace aot instrument` rewrites the application and the
assemblies it compiles with it (the framework and the packages) with the Datadog native tracer hosted offline, like the
profiler does at runtime. It also generates the code the instrumentation needs instead of dynamic code: the CallTarget
registrations and a DuckType AOT registry. The rewritten assemblies replace ILC's inputs, and a module initializer of the
application starts the tracer.

## Supported products

| Product | NativeAOT |
|---|---|
| APM: automatic instrumentation of the supported libraries, manual API, OpenTelemetry API, trace annotations | Yes |
| `DD_TRACE_METHODS` | Yes, from the publish (see `DatadogAotTraceMethods`) |
| App and API Protection: WAF, RASP | Yes |
| App and API Protection: IAST | Yes, opt-in at publish (`DatadogAotCategories`) |
| Data Streams Monitoring, runtime metrics, logs injection and direct log submission, Remote Configuration | Yes |
| Code Origin for spans | Yes (entry spans) |
| Continuous Profiler, Dynamic Instrumentation, Exception Replay | No |

Libraries or features that don't work with NativeAOT behave the same with and without the tracer.

## Differences with the instrumentation at runtime

- What is instrumented is decided at publish: integrations, `DD_TRACE_METHODS`, IAST. The settings the integrations read
  at runtime still apply (e.g. `DD_TRACE_<INTEGRATION>_ENABLED=false` stops creating their spans).
- Stack frames (error stacks, RASP stacks, IAST locations) have file and line with .NET 11 and later (see
  `StackTraceLineNumberSupport`). Earlier versions of NativeAOT have no line information at runtime: the frames have the
  method only.
- Duck typing (how the tracer reads the types of the libraries it instruments) can't create code at runtime: the package
  brings the mappings of the supported libraries, generated at publish. For mappings created from types only known at
  runtime, record them under JIT and pass them to the publish (see [Duck typing mappings](#duck-typing-mappings)).

## Duck typing mappings

The package has a catalog of the duck typing mappings the supported integrations create from runtime types. When an
application uses a library version or a code path the catalog doesn't cover, the tracer logs that a duck typing mapping is
missing and the integration is disabled. To add them, run the application under JIT with the tracer attached and
`DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH=<file>`, which records every mapping created, and pass the file to the publish:

```xml
<PropertyGroup>
  <DatadogAotDuckTypeMaps>ducktype-maps.json</DatadogAotDuckTypeMaps>
</PropertyGroup>
```

## Binary size

The instrumentation adds the tracer to the application: around 26 MB for a console application (7 MB without it), around
30 MB for an ASP.NET Core application with AppSec (12 MB without it), on linux-x64. `StackTraceLineNumberSupport`
(.NET 11) adds a few percent more.

## Troubleshooting

- Each publish writes a report of the instrumentation to `obj/<configuration>/<framework>/<rid>/datadog-aot/report.json`
  (instrumented assemblies and methods, CallTarget registrations, duck typing mappings and their compatibility, warnings).
- When the instrumentation fails, the application is published without it, with a warning:
  `DatadogAotFailOnError=true` fails the publish instead.
- `DatadogAotArguments=--verbose` logs the details of the instrumentation in the build output.
- At runtime, the tracer logs to the usual directory (`DD_TRACE_LOG_DIRECTORY`); `DD_TRACE_DEBUG=true` for details.

## Platforms

The instrumentation runs on the machine that publishes (the build host) and the package brings the native tracer for
`win-x64`, `win-x86`, `linux-x64`, `linux-musl-x64`, `linux-arm64`, `linux-musl-arm64` and macOS (x64 and Apple
silicon). On other build hosts the publish goes on without the instrumentation, with a warning. The application can target
any of those runtime identifiers.

## Properties

| Property | Description |
|---|---|
| `DatadogAotInstrumentation` | `false` to disable the instrumentation. |
| `DatadogAotDuckTypeMaps` | ducktype-aot map files recorded at runtime with `DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH` (`;`-separated): the duck typing mappings created from runtime types, in addition to the package's catalog (`DatadogAotBuiltInDuckTypeMaps=false` leaves it out). |
| `DatadogAotCategories` | Instrumentation categories (default `tracing,appsec,rasp`; add `iast` to opt in). |
| `DatadogAotFailOnError` | `true` to fail the publish when the instrumentation fails. By default, the application is published without it, with a warning. |
| `DatadogAotTraceMethods` | Methods to trace, with the `DD_TRACE_METHODS` syntax: they are instrumented when publishing (default: `DD_TRACE_METHODS` of the build). |
| `DatadogAotArguments` | Extra arguments for `dd-trace aot instrument` (e.g. `--verbose`). |
| `StackTraceLineNumberSupport` | .NET 11+: file and line in stack frames (error stacks, RASP stacks, IAST locations), like under JIT. The package sets it to `true` by default; `false` saves its size (a few percent of the binary). Earlier versions of NativeAOT have no line information. |
