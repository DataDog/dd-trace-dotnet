# Datadog.Trace.Annotations NuGet package

This package contains custom attribute types to enable additional features of the Datadog APM instrumentation library.

> Note: Automatic instrumentation is required for the attributes in this package to take effect. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation.

> Note: If you are unable to add new package references to your application, you may still enable this functionality by defining types inside your application whose full name and type members match the definitions in this package.

## Attributes
### Datadog.Trace.Annotations.TraceAttribute
An attribute that marks the decorated method to be instrumented by Datadog automatic instrumentation. [Source](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer/src/Datadog.Trace.Annotations/TraceAttribute.cs)

```csharp
using System;

namespace Datadog.Trace.Annotations;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TraceAttribute : Attribute
{
    public string OperationName { get; set; }

    public string ResourceName { get; set; }
}
```

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
