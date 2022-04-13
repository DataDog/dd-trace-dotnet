# Datadog.Trace.Annotations NuGet package

This package contains custom attribute types to enable additional features of the Datadog APM instrumentation library.

> Note: Datadog automatic instrumentation does not rely on importing this package to work. If you are unable to add new package references to your application, you may still enable this functionality by defining types inside your application with matching namespace-qualified type names and matching type members.

## Attributes

- [Datadog.Trace.Annotations.TraceAttribute](../../tracer/src/Datadog.Trace.Annotations/TraceAttribute.cs): An attribute that marks the decorated method to be instrumented by Datadog automatic instrumentation.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
