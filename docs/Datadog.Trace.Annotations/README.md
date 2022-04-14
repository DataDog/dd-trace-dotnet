# Datadog.Trace.Annotations NuGet package

This package contains custom attribute types to enable additional features of the Datadog APM instrumentation library.

> Note: Automatic instrumentation is required for the attributes in this package to take effect. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation.

> Note: If you are unable to add new package references to your application, you may still enable this functionality by defining types inside your application whose full name and type members match the definitions in this package.

## Attributes

- [Datadog.Trace.Annotations.TraceAttribute](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer/src/Datadog.Trace.Annotations/TraceAttribute.cs): An attribute that marks the decorated method to be instrumented by Datadog automatic instrumentation.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
