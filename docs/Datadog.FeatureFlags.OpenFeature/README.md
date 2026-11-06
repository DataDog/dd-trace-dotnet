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

## Flag selection

A service that needs only some of its flags can load only those, by tag, by key pattern, or both.
Datadog filters the flag configuration before sending it, so the service downloads and holds less.

```csharp
using Datadog.FeatureFlags.OpenFeature;

var provider = new DatadogProvider(new DatadogProviderOptions
{
    FlagSelection = new FlagSelection
    {
        Tags = ["team:payments"],
        KeyPatterns = ["checkout.*"],
    },
});
await OpenFeature.Api.Instance.SetProviderAsync(provider);
```

- A flag loads when it has any of the tags or its key matches any of the patterns. Tags match
  exactly. A pattern matches the whole key, `*` matches any run of characters, and every other
  character is literal. Matching is case-sensitive.
- Evaluating a flag outside the selection returns the default value with `FLAG_NOT_FOUND`. A
  selection that matches no flag loads no flags.
- Each list takes at most 31 values of at most 200 UTF-8 bytes each, after surrounding white space
  is trimmed, and the request URL that carries the selection is at most 8192 bytes. A selection
  outside these limits, or with a `null` value, is not sent: every flag loads, and the tracer log
  names the limit.
- The selection is copied when the provider is constructed. The first provider constructed in a
  process decides the selection for every provider in it, until all of them have shut down; a
  later provider with a different selection logs a warning and uses the first one.
- The selection applies to the default agentless configuration source. With the Remote Config
  source, every flag loads and a warning is logged.
- This needs tracer 3.58.0 or later. With an older tracer, the selection is not sent and every flag
  loads, without a warning.

## Exposures in your own data store

The provider sends exposures to Datadog. To record exposures in your own data store, use
`ExposureHook`. It receives every completed evaluation, with the same exposure decision that the
provider uses for Datadog, and the features of the selected split.

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
- After the tracer shuts down, exposures are no longer recorded, so evaluations get no decision:
  `ShouldSend` and `IsCacheHit` are false.
- An exception from the callback does not affect the evaluation. The first one is logged at warn
  level, later ones at debug level; pass an `ILogger` to the hook to route them, otherwise they go
  to `System.Diagnostics.Trace`.

### When exposures are sent

The provider records and sends an exposure only after OpenFeature has completed the evaluation
without an error, so an evaluation that a later hook fails sends and records nothing. This needs a
tracer version that supports it; with an older tracer, the tracer sends the exposure during the
evaluation, before hooks run.

The exposure is sent from a provider hook. If you call the provider's `Resolve...ValueAsync` methods
directly instead of through an OpenFeature client, no hooks run and no exposure is sent or recorded.
The synchronous methods below run the provider's hooks and do send exposures.

### Features

`Features` returns the features of the selected split whose destinations include the customer
hook, keyed without the `__dd_feature.` prefix. Values are a `string`, `double` or `bool`, with the
type the feature has in the flag configuration: a string feature whose value is `"true"` or
`"2026"` stays a `string`. The map is empty when the split has no such features, and with a tracer
that does not report split features. A tracer that reports features but not their types gives
every value as a `string`. Datadog can add destinations later; the tracer ignores a destination it
does not know and logs a warning once for each one.

### Flag metadata

The public flag metadata is the `__dd_feature.<key>` entries. There is one entry for each feature of
the selected split that lists the `HOOK` destination. The value is a `string`, `double` or `bool`.
Read these entries in the hook's `finally` stage, and skip details that have an error.

Other keys that start with `__dd_` are internal. They can change, and they can be missing. Do not
read the exposure decision from them. Use `ExposureHook` to get the exposure decision: `IsExposure`,
`IsCacheHit` and `ShouldSend`.

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
