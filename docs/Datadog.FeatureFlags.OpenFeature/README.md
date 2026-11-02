# `Datadog.FeatureFlags.OpenFeature` NuGet package

This package contains the OpenFeature provider for Datadog .NET Feature Flags Events platform (FFE).

## What does Datadog.Trace.OpenFeature contain?

Datadog.Trace.OpenFeature contains two things:

- A reference to the [OpenFeature v2 NuGet package](https://www.nuget.org/packages/OpenFeature) for custom instrumentation.
- The native binaries required for automatic instrumentation, for the Continuous Profiler, and for ASM.

These native binaries are identical to those installed by the MSI and Linux installer packages, so Datadog.Trace.Bundle should be considered an alternative deployment mechanism for automatic instrumentation. 

The main advantages of Datadog.Trace.Bundle over the MSI or Linux packages are:
- You can use it in locations where you cannot access the underlying host to install the MSI or Linux package.
- You can have multiple applications on the same host using different versions of Datadog.Trace.Bundle without issue.

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. *Configure the tracer* as shown in the next section
3. Configure the Datadog OpenFeature SDK [as described in our documentation](https://docs.datadoghq.com/feature_flags/server/dotnet/).
    1. Add the [Datadog.FeatureFlags.OpenFeature](https://www.nuget.org/packages/Datadog.FeatureFlags.OpenFeature) NuGet package to your project, using `dotnet add package Datadog.FeatureFlags.OpenFeature`, for example.
    2. Add the `Datadog.FeatureFlags.OpenFeature.DatadogProvider` to the `OpenFeature.Api.Instance`.
4. Use the `OpenFeature.FeatureClient` to retrieve configured feature flags from Datadog's backend.
5. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

### Configure the tracer

After adding the NuGet package to your project, set the following **required** environment variables to enable automatic instrumentation of your application and restart the application.

> **_NOTE:_** 
The following are the mandatory variables. For further configuration options, see our public documentation for the [Tracer](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/?tab=environmentvariables) and the [Continuous Profiler](https://docs.datadoghq.com/profiler/enabling/dotnet/?tab=linux#configuration).


.NET Core:

```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
CORECLR_PROFILER_PATH=<System-dependent path>
DD_DOTNET_TRACER_HOME=<APP_DIRECTORY>/datadog
```

.NET Framework:

```
COR_ENABLE_PROFILING=1
COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
COR_PROFILER_PATH=<System-dependent path>
DD_DOTNET_TRACER_HOME=<APP_DIRECTORY>/datadog
```

The value for the `<APP_DIRECTORY>` placeholder is the path to the directory containing the application’s .dll files. The value for the `CORECLR_PROFILER_PATH`/`COR_PROFILER_PATH` environment variable varies based on the system where the application is running:

| OPERATING SYSTEM AND PROCESS ARCHITECTURE | CORECLR_PROFILER_PATH VALUE                                                  |
|-------------------------------------------|------------------------------------------------------------------------------|
| Alpine Linux x64                          | <APP_DIRECTORY>/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so   |
| Linux x64                                 | <APP_DIRECTORY>/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so        |
| Alpine Linux ARM64                        | <APP_DIRECTORY>/datadog/linux-musl-arm64/Datadog.Trace.ClrProfiler.Native.so |
| Linux ARM64                               | <APP_DIRECTORY>/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so      |
| Windows x64                               | <APP_DIRECTORY>\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll         |
| Windows x86                               | <APP_DIRECTORY>\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll         |

For Docker images running on Linux, configure the image to run the createLogPath.sh script:

```
RUN /<APP_DIRECTORY>/datadog/createLogPath.sh
```

### Examples

Docker examples are available [here](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer/samples/NugetDeployment)

## Exposures in your own data store

The provider sends exposures to Datadog. It also puts its exposure decision and the selected split's
features into the flag metadata of every evaluation, so any OpenFeature hook can record exposures
in your own data store (see [Flag metadata](#flag-metadata)).

For convenience, this package ships `ExposureHook`, which reads that metadata for you. It receives
every completed evaluation, with the same exposure decision the provider uses for Datadog.

```csharp
using Datadog.FeatureFlags.OpenFeature;
using OpenFeature;

var queue = new BlockingCollection<ExposureRecord>(10_000);

OpenFeature.Api.Instance.AddHooks(new ExposureHook(evaluation =>
{
    if (!evaluation.ShouldSend)
    {
        return;
    }

    var features = evaluation.Features;
    // TryAdd never blocks the evaluation: when the queue is full, the record is dropped.
    queue.TryAdd(new ExposureRecord(
        evaluation.FlagKey,
        evaluation.Variant,
        evaluation.TargetingKey,
        (string)features["holdout.key"],
        (string)features["holdout.assignment_group"]));
}));
// A background thread drains the queue and writes to the data store.
```

- The callback runs on the evaluation thread. Do not write to the data store from the callback:
  copy what you need and hand it to a background queue. Do not block: use a non-blocking add, so
  a slow data store cannot delay flag evaluations.
- `ShouldSend` is true for a successful evaluation of an allocation that logs exposures, for a
  subject not yet exposed to the same allocation, variant and serial id. These are exactly the
  exposures the provider sends to Datadog while Datadog logging is on. The serial id and the
  verdict require a tracer version that carries this feature; with an older tracer, `ShouldSend`
  is false.
- `IsExposure` is true for every successful evaluation of an allocation that logs exposures,
  including repeats. `IsCacheHit` tells repeats apart.
- An evaluation that fails, including one that an `after` hook fails, is not an exposure.
- An exception from the callback does not affect the evaluation. The first one is logged at warn
  level, later ones at debug level; pass an `ILogger` to the hook to route them, otherwise they go
  to `System.Diagnostics.Trace`.

### Features

`Features` returns the features of the selected split whose destinations include the customer
hook, keyed without the `__dd_feature.` prefix. Values are a `string`, `double` or `bool`, with the
type the feature has in the flag configuration: a string feature whose value is `"true"` or
`"2026"` stays a `string`. With an older tracer that does not report feature types, every value is
a `string`. The map is empty when the split has no such features. Datadog can add destinations later; the tracer
ignores a destination it does not know and logs a warning once for each one.

### Flag metadata

The same data is in the evaluation's flag metadata, for hooks that use only the OpenFeature API.
Read it in the hook's `finally` stage, where the details match what the application receives, and
skip details that have an error. `ExposureHook` does both.

| Key | Type | Meaning |
|---|---|---|
| `__dd_allocation_key` | String | The selected allocation |
| `__dd_do_log` | Boolean | Whether the allocation logs exposures. Present when the allocation logs exposures or span enrichment is on |
| `__dd_exposure_cache_hit` | Boolean | The subject was already exposed. Present only when `__dd_do_log` is true |
| `__dd_split_serial_id` | Integer | The split's serial id. Present when `__dd_do_log` is present and the split has a serial id |
| `__dd_feature.<key>` | String, Double or Boolean | One entry for each feature of the selected split that lists the `HOOK` destination |

The tracer and this package ship separately, and the tracer fills the metadata. An older tracer
emits fewer keys, and the hooks treat a missing key as its fail-safe value.

### Stop sending exposures to Datadog

```
DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED=false
```

The provider then sends no exposure events to Datadog. `ExposureHook` callbacks still receive every
evaluation, the exposure cache still records, and `ShouldSend` gives the same results. The default
is `true`.

This setting stops exposures only. Flag evaluation events have their own setting,
`DD_FLAGGING_EVALUATION_COUNTS_ENABLED`, and are unaffected.

## Synchronous evaluation (experimental)

`DatadogProvider` provides experimental synchronous methods for code that cannot
await: `ResolveBooleanValue`, `ResolveIntegerValue`, `ResolveDoubleValue`,
`ResolveStringValue`, and `ResolveStructureValue`. Each returns OpenFeature
`ResolutionDetails<T>` from the local evaluator. Register the provider with
`SetProviderAsync` first; initialization and configuration delivery remain asynchronous.

```csharp
using Datadog.FeatureFlags.OpenFeature;
using OpenFeature.Model;

var provider = new DatadogProvider();
await OpenFeature.Api.Instance.SetProviderAsync(provider);
var context = EvaluationContext.Builder().SetTargetingKey("customer-123").Build();

#pragma warning disable DDFF001 // Explicit opt-in to experimental sync resolution.
var result = provider.ResolveBooleanValue("new-checkout", false, context);
#pragma warning restore DDFF001

bool enabled = result.Value;
```

These methods are Datadog extensions, outside OpenFeature's `IFeatureClient` API.
They use only the context you pass; global, client, and transaction context are
not merged, and OpenFeature client lifecycle checks do not run. The provider's
own hooks run before the call returns, recording evaluation metrics on supported
frameworks and span enrichment when enabled. Hooks registered on the OpenFeature
API or client do not run. Evaluator-owned exposure recording remains enabled.

Until configuration arrives, a call returns your default value with
`ProviderNotReady`. Inspect the error details when a default is unsuitable.
Cancellation and invalid arguments retain the direct async provider methods'
exception behavior.

These APIs report diagnostic `DDFF001`; suppress it explicitly to opt in. Older
compilers may not enforce this diagnostic. Existing async calls require no opt-in.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
