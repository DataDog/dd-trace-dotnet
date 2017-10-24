# dd-trace-csharp

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace C# applications. Some quick notes up front:

- **Datadog C# APM is currently in Alpha**
- It support .Net Framework version above 4.5 and .Net Core 2.0
- It does not support out of process propagation
- It does not provide automatic framework instrumentation, all instrumentation is [manual](#manual-instrumentation)
- Multiple AppDomains are not supported (but it could work).
- It does not support OpenTracing `FollowsFrom` references, `Baggage` or `Log`


## Getting Started

Before instrumenting your code, [install the Datadog Agent](https://app.datadoghq.com/account/settings#agent) on your application servers (or locally, if you're just trying out C# APM) and enable the APM Agent. See the special instructions for [Windows](https://github.com/DataDog/datadog-trace-agent#run-on-windows) and [Docker](https://github.com/DataDog/docker-dd-agent#tracing--apm) if you're using either.

### Manual Instrumentation

#### Setup

#### Examples

## Further Reading

- [OpenTracing's documentation](https://github.com/opentracing/opentracing-csharp); feel free to use the Trace C# API to customize your instrumentation.
- [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/)
- [Datadog APM FAQ](https://docs.datadoghq.com/tracing/faq/)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions or feedback, email us at tracehelp@datadoghq.com or chat with us in the datadoghq slack channel #apm-csharp.
