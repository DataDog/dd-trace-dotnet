# dd-trace-csharp

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace Csharp applications. Two quick notes up front:

- **Datadog Csharp APM is currently in Alpha**
- ################# ANY LIMITATIONS ?? #####################

## The Components


**[Datadog Tracer](https://github.com/DataDog/dd-trace-csharp)**: an OpenTracing-compatible library that lets you trace any piece of your Csharp code, not just whole methods.

**[Datadog APM Agent](https://github.com/DataDog/datadog-trace-agent)**: a service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. (The APM Agent is not part of this repo; it's the same Agent to which all Datadog tracers—Go, Python, etc—send data)

## Getting Started

Before instrumenting your code, [install the Datadog Agent](https://app.datadoghq.com/account/settings#agent) on your application servers (or locally, if you're just trying out Csharp APM) and enable the APM Agent. See the special instructions for [Windows](https://github.com/DataDog/datadog-trace-agent#run-on-windows) and [Docker](https://github.com/DataDog/docker-dd-agent#tracing--apm) if you're using either.

### Automatic Tracing

The Csharp Agent—once passed to your application—automatically traces requests to the frameworks, application servers, and databases shown below. It does this by using various libraries from [opentracing-contrib](https://github.com/opentracing-contrib). In most cases you don't need to install or configure anything; traces will automatically show up in your Datadog dashboards.

#### Application Servers

| Server | Versions | Comments |
| ------------- |:-------------:| -----|
||||

#### Frameworks

| Framework        | Versions           | Comments  |
| ------------- |:-------------:| ----- |
||||

#### Databases


| Database      | Versions           | Comments  |
| ------------- |:-------------:| ----- |
||||

#### Setup

#### Example


### Manual Instrumentation

#### Setup

#### Examples


## Further Reading

- [OpenTracing's documentation](https://github.com/opentracing/opentracing-csharp); feel free to use the Trace Csharp API to customize your instrumentation.
- [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/)
- [Datadog APM FAQ](https://docs.datadoghq.com/tracing/faq/)

## Get in touch

If you have questions or feedback, email us at tracehelp@datadoghq.com or chat with us in the datadoghq slack channel #apm-Csharp.
