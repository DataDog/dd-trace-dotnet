# dd-trace-csharp

**Datadog .NET APM is currently in Alpha**

Tracing for .NET is Alpha and it is not recommended to be used in production. Please [check our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for more details.

## What is Datadog APM?

Datadog APM traces the path of each request through your application stack, recording the latency of each step along the way. It sends all tracing data to Datadog, where you can easily identify which services or calls are slowing down your application the most.

This repository contains what you need to trace .NET applications. Some quick notes up front:

- Supports .NET Framework 4.5 or newer
- Supports .NET Core 2.0 or newer
- Multiple AppDomains are not supported

## The Components


**[Datadog Tracer](https://github.com/DataDog/dd-trace-csharp)**: an OpenTracing-compatible .NET library that lets you trace any piece of your .NET code.

**[Datadog APM Agent](https://github.com/DataDog/datadog-trace-agent)**: a service that runs on your application servers, accepting trace data from the Datadog Tracer and sending it to Datadog. (The APM Agent is not part of this repo; it's the same Agent to which all Datadog tracers—Go, Python, etc—send data)

## Getting Started

Before instrumenting your code, [install the Datadog APM Agent](https://docs.datadoghq.com/tracing/setup/) on your application servers (or locally, if you're just trying out .NET APM).

## Development

### Dependencies

#### Windows

In order to build and run all the projects and tests included in this repo you need to have [Visual Studio 2017](https://visualstudio.microsoft.com/downloads/) and the [.NET Core 2.0 SDK](https://www.microsoft.com/net/download) or newer installed on your machine.

Some tests require you to have [Docker for Windows](https://docs.docker.com/docker-for-windows/) on your machine or to manually install the required dependencies.

#### Unix

Make sure you have installed:
- [.NET Core SDK](https://www.microsoft.com/net/download) (2.0 or newer)
- [Mono](https://www.mono-project.com/download/stable/)
- [Docker](https://www.docker.com/)

Because some projects target the desktop framework and of [this bug](https://github.com/dotnet/sdk/issues/335), you'll need [this workaround](https://github.com/dotnet/netcorecli-fsc/wiki/.NET-Core-SDK-rc4#using-net-framework-as-targets-framework-the-osxunix-build-fails) to make the build work.

### Setup

This project makes use of git submodules. This means that in order to start developing on this project, you should either clone this repository with the `--recurse-submodules` option or run the following commands in the cloned repository:

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

- [Datadog APM documentation](https://docs.datadoghq.com/tracing/)
- [Datadog - Tracing .NET Applications](https://docs.datadoghq.com/tracing/setup/dotnet/)
- [OpenTracing documentation](https://github.com/opentracing/opentracing-csharp)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)

## Get in touch

If you have questions or feedback, email us at tracehelp@datadoghq.com or chat with us in Slack channel `#apm-csharp` in [Datadog's public Slack](http://chat.datadoghq.com/).
