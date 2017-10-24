# dd-trace-csharp

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace C# applications. Some quick notes up front:

- **Datadog C# APM is currently in Alpha**
- It supports .Net Framework version above 4.5 and .Net Core 2.0
- It does not support out of process propagation
- It does not provide automatic framework instrumentation, all instrumentation is [manual](#manual-instrumentation)
- Multiple AppDomains are not supported (but it could work).
- Our tracer is based on the current OpenTracing standard, however we do not yet support the following features: `FollowsFrom` references, `Baggage` or `Log`.

## The Components


**[Datadog Tracer](https://github.com/DataDog/dd-trace-csharp)**: an OpenTracing-compatible library that lets you trace any piece of your C# code.

**[Datadog APM Agent](https://github.com/DataDog/datadog-trace-agent)**: a service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. (The APM Agent is not part of this repo; it's the same Agent to which all Datadog tracers—Go, Python, etc—send data)

## Getting Started

Before instrumenting your code, [install the Datadog Agent](https://app.datadoghq.com/account/settings#agent) on your application servers (or locally, if you're just trying out C# APM) and enable the APM Agent. On Windows, the trace agent is shipped together with the Datadog Agent only since version 5.18.2, so users must update to 5.18.2 or above. See special instructions for [Docker](https://github.com/DataDog/docker-dd-agent#tracing--apm) if you're using it.

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
