# dd-trace-csharp

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace C# applications. Some quick notes up front:

- **Datadog C# APM is currently in Alpha**
- It supports .Net Framework version above 4.5 and .Net Core 2.0.
- It does not support out of process propagation.
- It does not provide automatic framework instrumentation, all instrumentation is [manual](#manual-instrumentation).
- Multiple AppDomains are not supported.
- Our tracer is based on the current OpenTracing standard, however we do not yet support the following features: `FollowsFrom` references, `Baggage` or `Log`.

## The Components


**[Datadog Tracer](https://github.com/DataDog/dd-trace-csharp)**: an OpenTracing-compatible library that lets you trace any piece of your C# code.

**[Datadog APM Agent](https://github.com/DataDog/datadog-trace-agent)**: a service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. (The APM Agent is not part of this repo; it's the same Agent to which all Datadog tracers—Go, Python, etc—send data)

## Getting Started

Before instrumenting your code, [install the Datadog Agent](https://app.datadoghq.com/account/settings#agent) on your application servers (or locally, if you're just trying out C# APM) and enable the APM Agent. On Windows, please see the instructions below. See special instructions for [Docker](https://github.com/DataDog/docker-dd-agent#tracing--apm) if you're using it.

### Windows

On Windows, the trace agent is shipped together with the Datadog Agent only since version 5.19.0, so users must update to 5.19.0 or above. However the Windows trace agent is in beta and some manual steps are required.

Update your config file to include:

```
[Main]
apm_enabled: yes
[trace.config]
log_file = C:\ProgramData\Datadog\logs\trace-agent.log
```

Restart the datadogagent service:

```
net stop datadogagent
net start datadogagent
```

For this beta the trace agent status and logs are not displayed in the Agent Manager GUI.

To see the trace agent status either use the Service tab of the Task Manager or run:

```
sc.exe query datadog-trace-agent
```

And check that the status is "running".

The logs are available at the path you configured in `trace.config` `log_file` above.

### Manual Instrumentation

#### Introduction

Before instrumenting your application, have a look at the [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/) to get familiar with the core concepts of Datadog APM.

#### Setup

In order to instrument you code you need to add the `Datadog.Trace` NuGet package to your project.

Your tracing adventure starts with the `ITracer` object, you should typically instantiate only one `ITracer` for the lifetime of your app and use it in all places of your code where you want to add tracing. Instantiating the `ITracer` is done with the `TracerFactory.GetTracer` method.

To get a tracer with default parameters (i.e. the agent endpoint set to `http://localhost:8126`, and the default service name set to the name of the AppDomain):

```csharp
ITracer tracer = TracerFactory.GetTracer();
```

Customize your tracer object by adding optional parameters to the `TracerFactory.GetTracer` call:

By default the service name is set to the name of the AppDomain, choose a custom name with the defaultServiceName parameter:

```csharp
ITracer tracer = TracerFactory.GetTracer(defaultServiceName: "YourServiceName")
```

By default, the trace endpoint is set to http://localhost:8126, send traces to a different endpoint with the agentEndpoint parameter:

```csharp
ITracer tracer = TracerFactory.GetTracer(agentEndpoint: new Url("http://myendpoint:port"));
```

#### Examples

Use the shared `ITracer` object you created to create spans, instrument any section of your code, and get detailed metrics on it.

Set the ServiceName to recognize which service this trace belongs to; if you don't, the parent span's service name or in case of a root span the defaultServiceName stated above is used.

Set the ResourceName to scope this trace to a specific endpoint or SQL Query; For instance:
- "GET /users/:id"
- "SELECT * FROM ..."
if you don't the OperationName will be used.

A minimal examples is:

```csharp
using (ISpan span = tracer.BuildSpan("OperationName").WithTag(DDTags.ServiceName, "ServiceName").Start())
{
    span.SetTag(DDTags.ResourceName, "ResourceName");

    // Instrumented code
    Thread.Sleep(1000);
}
```

You may also choose, not to use the `using` construct and close the `ISpan` object explictly:

```csharp
ISpan span = tracer.BuildSpan("OperationName").WithTag(DDTags.ServiceName, "ServiceName").Start();
span.SetTag(DDTags.ResourceName, "ResourceName");

// Instrumented code
Thread.Sleep(1000);


// Finish sets the span duration and sends it to the agent (if you don't call finish the data will never be sent to Datadog)
span.Finish();
```

You may add custom tags by calling `ISpan.SetTag`:

```csharp
ISpan span = tracer.BuildSpan("SqlQuery").Start();
span.SetTag("db.rows", 10);
```

You should not have to explicitly declare parent/children relationship between your spans, but to override the default behavior - a new span is considered a child of the innermost open span in its logical context- use:

```csharp
ISpan parent = tracer.BuildSpan("Parent").Start();
ISpan child = tracer.BuildSpan("Child").AsChildOf(parent).Start();
```

#### Cross-process tracing

Cross-process tracing is supported by propagating context through HTTP headers. This is done with the `ITracer.Inject` and `ITracer.Extract` methods as documented in the [opentracing documentation](http://opentracing.io/documentation/pages/api/cross-process-tracing.html)

##### Injection

Example code to send a HTTP request with the right headers:

```csharp
using (var span = tracer.BuildSpan("Operation").Start())
{
    var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com");
    var carrier = new HttpHeadersCarrier(_request.Headers);
    tracer.Inject(span.Context, Formats.HttpHeaders, carrier);
    await client.SendAsync(request);
}
```

##### Extraction

In order to extract context from a http request, you need to write a wrapper
around the header container used by your web framework to make it implement the
`ITextMap` interface.

For example such a wrapper for the `IHeaderDictionary` used by Asp.Net Core could be:

```csharp
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using OpenTracing.Propagation;

public class AspNetHeadersTextMap : ITextMap
{
    private readonly IHeaderDictionary _headers;

    public AspNetHeadersTextMap(IHeaderDictionary headers)
    {
        _headers = headers;
    }

    public string Get(string key)
    {
        return _headers[key];
    }

    public IEnumerable<KeyValuePair<string, string>> GetEntries()
    {
        return (IEnumerable<KeyValuePair<string, string>>)_headers;
    }

    public void Set(string key, string value)
    {
        _headers[key] = value;
    }
}
```

You can then leverage the extract method to extract the cross-process
correlation context from a request's headers:

```csharp
var tracer = TracerFactory.GetTracer();
var headersTextMap = new AspNetHeadersTextMap(HttpContext.Request.Headers);
var spanContext = tracer.Extract(Formats.HttpHeaders, headersTextMap);
using (var span = tracer.BuildSpan("Operation").AsChildOf(spanContext).Start())
{
    // Your instrumented code
}
```

#### Advanced Usage

When creating a tracer, add some metadata to your services to customize how they will appear in your Datadog application:

```csharp
var serviceInfoList = new List<ServiceInfo>
{
    new ServiceInfo
    {
        App = "MyAppName",
        AppType = "web",
        ServiceName = "MyServiceName"
    }
};
ITracer tracer = TracerFactory.GetTracer(serviceInfoList: serviceInfoList);
```

## Development

### Dependencies

In order to build and run all the projects and test included in this repo you need to have Visual Studio 2017 as well as the .Net Core 2.+ SDK installed on your machine.

Alternatively for non Windows users, it's possible to build the library only for netstandard2.0 and run the tests for the .net core runtime. This should prove enough for most development tasks and the CI will run the tests on the full .Net Framework.

### Setup

This project makes use of git submodules. This means that in order to start developping on this project, you should either clone this repository with the `--recurse-submodules` option or run the following commands in the cloned repository:

```
git submodule init
git submodule update

```

### Running tests

If you are using Visual Studio, the tests should appear in the test explorer and can be run from there.

The tests contained in the projects "Datadog.Trace.IntegrationTests" and "Datadog.Trace.IntegrationTests.Net45" require to have the Datadog trace agent running on the same machine and listening on the port 8126.

If you are not using Visual Studio, you can run the tests in "Datadog.Trace.Tests" and "Datadog.Trace.IntegrationTests" with the `dotnet test` command.

## Further Reading

- [OpenTracing's documentation](https://github.com/opentracing/opentracing-csharp); feel free to use the Trace C# API to customize your instrumentation.
- [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/)
- [Datadog APM FAQ](https://docs.datadoghq.com/tracing/faq/)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions or feedback, email us at tracehelp@datadoghq.com or chat with us in the datadoghq slack channel #apm-csharp.
