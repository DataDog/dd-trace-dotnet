# Datadog.Trace.BenchmarkDotNet NuGet package

This package contains the BenchmarkDotNet exporter and instrumentation for [Datadog CI Visibility](https://docs.datadoghq.com/continuous_integration/).

## Compatibility

- `BenchmarkDotNet 0.13.2` and above

## Getting Started

1. Add the `Datadog.Trace.BenchmarkDotNet` NuGet package to your project, using `dotnet add package Datadog.Trace.BenchmarkDotNet`, for example.
2. Configure your project to use the Datadog.Trace.BenchmarkDotNet exporter, as [described below](#configure-the-project)
3. Configure your CI provider to report via the Datadog Agent or agentless, [as described in the documentation](https://docs.datadoghq.com/continuous_integration/tests/dotnet/?tab=onpremisesciproviderdatadogagent#configuring-reporting-method)
4. Run the benchmark project and check results on [Datadog CI Test Visibility](https://docs.datadoghq.com/continuous_integration/tests/).

### Configure the project

There's two way to configure a benchmark project to use the Datadog's exporter:

#### By Attribute

Add the `DatadogDiagnoser` attribute to the benchmark class.

```csharp
using BenchmarkDotNet.Attributes;
using Datadog.Trace.BenchmarkDotNet;

[DatadogDiagnoser]
[MemoryDiagnoser]
public class OperationBenchmark
{
    [Benchmark]
    public void Operation()
    {
        // ...
    }
}
```

#### By Configuration

Use the `WithDatadog()` extension method on the current project configuration:

```csharp
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Datadog.Trace.BenchmarkDotNet;

var config = DefaultConfig.Instance
    .WithDatadog();

BenchmarkRunner.Run<OperationBenchmark>(config);
```

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
