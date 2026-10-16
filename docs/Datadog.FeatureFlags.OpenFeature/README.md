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

## Per-subject evaluation telemetry opt-out

Enable the parameter when you construct the provider. Then set the exact context
key `dd_evaluation_telemetry_opt_out` to boolean `true` for an opted-out subject.

```csharp
using Datadog.FeatureFlags.OpenFeature;
using OpenFeature.Model;

var provider = new DatadogProvider(new DatadogProviderOptions
{
    EnableEvaluationTelemetryOptOutParameter = true,
});
await OpenFeature.Api.Instance.SetProviderAsync(provider);
var context = EvaluationContext.Builder()
    .SetTargetingKey("subject-123")
    .Set("dd_evaluation_telemetry_opt_out", true)
    .Build();
var enabled = await OpenFeature.Api.Instance.GetClient()
    .GetBooleanValueAsync("new-checkout", false, context);
```

Alternatively, set `DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED=true`
and use `new DatadogProvider()` without an explicit option.
An explicit `true` or `false` option overrides the environment setting.
An unset option uses the environment. Missing or invalid environment values disable the parameter.
The provider copies the resolved setting at construction. Later environment or option changes do not affect it.
Activation alone does not suppress telemetry; each subject must supply the boolean context value.
Only boolean `true` suppresses new Datadog evaluation metrics and automatic flag
span annotations. Missing values, strings, numbers, null, and boolean `false`
preserve telemetry. The provider does not currently produce evaluation EVP events.

Flag results, randomization, experiment exposures, exposure deduplication, and
application hooks remain unchanged. Previously recorded telemetry remains available.
The asynchronous client uses the merged evaluation context, including successful
Before hook changes. Experimental synchronous methods use only their supplied context.

OpenFeature 2.3.0 has one error-path limitation. If one application Before hook
changes the context and a later Before hook throws, the provider receives only
the original context. Evaluation does not run and no exposure is produced.
An error metric and runtime-default span annotation can still be recorded if
the original context did not opt out. Set opt-out in the supplied evaluation
context when you need suppression on this path.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
