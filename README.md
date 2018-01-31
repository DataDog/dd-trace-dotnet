# dd-trace-csharp

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace C# applications. Some quick notes up front:

- **Datadog C# APM is currently in Alpha**
- It supports .Net Framework version above 4.5 and .Net Core 2.0.
- It does not support out of process propagation.
- Multiple AppDomains are not supported.

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

### Automatic Instrumentation

#### ASP.NET Core

To instrument you ASP.NET Core application install the
`Datadog.Trace.AspNetCore` NuGet package and the following line to your
`ConfigureServices` method:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services
        .AddDatadogTrace()
}
```

Once your application is configured this way all the requests to your
application will be traced and the active span will automatically be set to the
currently executing request.

#### Ado.Net / System.Data.SqlClient

To instrument Ado.Net to trace all the SQL queries made by your application
install the `Datadog.Trace.SqlClient` NugGet package and execute the
following code at application startup:

```csharp
SqlClientIntegration.Enable()
```

### Manual Instrumentation

#### Introduction

Before instrumenting your application, have a look at the [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/) to get familiar with the core concepts of Datadog APM.

#### Setup

In order to instrument your code you need to add the `Datadog.Trace` NuGet
package to your project.

Your tracing adventure starts with the `Tracer` class that will be used to
instrument your code and should be accessed exclusively through the
`Tracer.Instance` singleton. `Trace.Instance` is statically initialized with a
`Tracer` created with the default settings but you may instantiate a new one
with customized values with the `Tracer.Create` method.

`Tracer.Create` takes a number of optional parameters that can be used to
customize the returned `Tracer`:

- agentEndpoint: the agent endpoint where the traces will be sent (default is http://localhost:8126)
- defaultServiceName: default name of the service (default is the name of the executing assembly)
- isDebugEnabled: turns on all debug logging, this may have an impact on application performance (default is false)

For example to set a custom service name:

```csharp
Tracer.Instance = Tracer.Create(defaultServiceName: "YourServiceName")
```

#### In process propagation

We want to keep track of the dependencies between spans created inside a
process. This is done automatically by the tracer when using the `StartActive`
method that returns a Scope representing the scope in which the created Span is
considered active. All Spans created without `ignoreActiveScope = true` are
automatically parented to the current active Span and become themselves active
(unless the `StartSpan` method is used).

If not created with `finishOnClose = false` closing a Scope will also close the
Span it is enclosing.

Examples:

```csharp
// The second span will be a child of the first one.
using (Scope scope = Tracer.Instance.StartActive("Parent")){
    using(Scope scope = Tracer.Instance.StartActive("Child")){
    }
}
```

```csharp
// Since it is created with the StartSpan method the first span is not made
// active and the second span will not be parented to it.
using (Span span = Tracer.Instance.StartSpan("Span1")){
    using(Scope scope = Tracer.Instance.StartActive("Span2")){
    }
}
```

#### Code instrumentation

Use the shared `Tracer` object you created to create spans, instrument any
section of your code, and get detailed metrics on it.

Set the ServiceName to recognize which service this trace belongs to; if you
don't, the parent span's service name or in case of a root span the
defaultServiceName stated above is used.

Set the ResourceName to scope this trace to a specific endpoint or SQL Query; For instance:
- "GET /users/:id"
- "SELECT * FROM ..."
if you don't the OperationName will be used.

A minimal example is:

```csharp
using (Scope scope = Tracer.Instance.StartActive("OperationName", serviceName: "ServiceName"))
{
    scope.Span.ResourceName = "ResourceName";

    // Instrumented code
    Thread.Sleep(1000);
}
```

You may also choose, not to use the `using` construct and close the `Scope` object explictly:

```csharp
Scope scope = Tracer.Instance.StartActive("OperationName", serviceName: "ServiceName");
scope.Span = "ResourceName";

// Instrumented code
Thread.Sleep(1000);


// Close closes the underlying span, this sets its duration and sends it to the agent (if you don't call Close the data will never be sent to Datadog)
scope.Close();
```

You may add custom tags by calling `Span.SetTag`:

```csharp
Scope scope = Tracer.Instance.StartActive("SqlQuery");
scope.Span.SetTag("db.rows", 10);
```

You should not have to explicitly declare parent/children relationship between your spans, but to override the default behavior - a new span is considered a child of the active Span - use:

```csharp
Span parent = tracer.StartSpan("Parent");
Span child = tracer.StartSpan("Child", childOf: parent.Context);
```

## Development

### Dependencies

#### Windows

In order to build and run all the projects and test included in this repo you need to have Visual Studio 2017 as well as the .Net Core 2.+ SDK installed on your machine.

Some tests require you to have enable docker support on your machine or to manually install the required dependencies.

#### Unix

Make sure you have installed:
- The .Net Core 2 SDK 
- Mono
- Docker

Because some projects target the desktop framework and of [this bug](https://github.com/dotnet/sdk/issues/335), you'll need [this workaround](https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-rc4#using-net-framework-as-targets-framework-the-osxunix-build-fails) to make the build work.

### Setup

This project makes use of git submodules. This means that in order to start developping on this project, you should either clone this repository with the `--recurse-submodules` option or run the following commands in the cloned repository:

```
git submodule init
git submodule update

```

### Running tests

The tests require the dependencies specified in `docker-compose.yaml` to be running on the same machine.
For this you need to have docker installed on your machine, and to start the dependencies with `./build.sh --target=dockerup`.

To build and run the tests on Windows:

```
./build.ps1
```

Or on Unix systems:

```
./build.sh
````

## Further Reading

- [OpenTracing's documentation](https://github.com/opentracing/opentracing-csharp); feel free to use the Trace C# API to customize your instrumentation.
- [Datadog APM Terminology](https://docs.datadoghq.com/tracing/terminology/)
- [Datadog APM FAQ](https://docs.datadoghq.com/tracing/faq/)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions or feedback, email us at tracehelp@datadoghq.com or chat with us in the datadoghq slack channel #apm-csharp.
