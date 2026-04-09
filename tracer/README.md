# dd-trace-dotnet/tracer

This folder contains the source code for the Datadog .NET APM Tracer. The .NET Tracer automatically instruments supported libraries out-of-the-box and also supports custom instrumentation to instrument your own code.

## Installation and usage

### Getting started

Configure the Datadog Agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm). For automatic instrumentation, install and enable the .NET Tracer [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer).

### Custom instrumentation

The Datadog .NET APM Tracer allows you to manually instrument your application (in addition to automatic instrumentation). To use it, please follow [the NuGet package documentation](https://github.com/DataDog/dd-trace-dotnet/tree/master/docs/Datadog.Trace/README.md).

## Automatic instrumentation

The .NET Tracer automatically instruments many popular libraries out-of-the-box, with no code changes required. When automatic instrumentation is enabled, the tracer intercepts calls to supported libraries and generates spans with timing, tags, and error information.

### Automatically instrumented libraries

| Category | Libraries |
|----------|-----------|
| **HTTP clients** | `System.Net.Http.HttpClient` / `HttpMessageHandler` 4.0+ |
| **Web frameworks** | ASP.NET (MVC, Web API, Web Forms), ASP.NET Core |
| **SQL databases** | `System.Data.SqlClient` 4.0+, `Microsoft.Data.SqlClient` 1.0+, `Npgsql` 4.0+, `MySql.Data` 6.7+, `MySqlConnector` 0.61+, `System.Data.SQLite` 2.0+, `Oracle.ManagedDataAccess` 4.122+ |
| **NoSQL databases** | `MongoDB.Driver.Core` 2.1+, `StackExchange.Redis` 1.0.187+, `ServiceStack.Redis` 4.0.48+, `Elasticsearch.Net` 5.3+, `CouchbaseNetClient` 2.2.8+ |
| **Messaging** | `RabbitMQ.Client` 3.6.9+, `Confluent.Kafka` 1.4+, `AWSSDK.SQS` 3.0+, `AWSSDK.SNS` 3.0+, `Azure.Messaging.ServiceBus` 7.14+, `IBM MQ` 9.0+ |
| **Logging** | Serilog 1.4+, NLog 4.0+, log4net 1.0+, `Microsoft.Extensions.Logging` 2.0+ |
| **gRPC** | `Grpc.Net.Client` 2.30+, `Grpc.Core` 2.30+ |
| **GraphQL** | `GraphQL` 2.3+, `HotChocolate` 11.0+ |
| **AWS SDK** | `AWSSDK.Core` 3.0+ (DynamoDB, S3, Kinesis, etc.) |
| **Azure SDK** | `Azure.Messaging.EventHubs` 5.9.2+ |

For the complete and up-to-date list, see the [Compatibility Requirements](https://docs.datadoghq.com/tracing/trace_collection/compatibility/dotnet-core/) documentation.

### How HTTP client tracing works

When automatic instrumentation is enabled, all `HttpClient` calls are automatically traced and trace context is propagated to downstream services. No code changes are required:

```csharp
// With automatic instrumentation enabled, this HttpClient call is automatically traced.
// A span is created with the HTTP method, URL, status code, and timing.
// Trace context headers (Datadog, W3C tracecontext, baggage) are automatically
// injected into the outgoing request so the downstream service can continue the trace.
var client = new HttpClient();
var response = await client.GetAsync("https://api.downstream-service.com/orders");
```

The tracer automatically:
- Creates a span for the HTTP call with operation name, URL, method, and status code
- Injects trace context headers (`x-datadog-trace-id`, `traceparent`, etc.) into the outgoing request
- Propagates sampling decisions to downstream services
- Marks the span as an error if the response status code indicates a client or server error

### How database tracing works

ADO.NET-based database calls are also automatically instrumented:

```csharp
// With automatic instrumentation enabled, this SQL query is automatically traced.
// A span is created capturing the query text, database name, execution time, and row count.
using var connection = new SqlConnection(connectionString);
connection.Open();

using var command = new SqlCommand("SELECT * FROM Orders WHERE CustomerId = @id", connection);
command.Parameters.AddWithValue("@id", customerId);

using var reader = command.ExecuteReader();
```

For database clients that are **not** automatically instrumented, you can [manually wrap the call in a span](../docs/Datadog.Trace/README.md#instrument-a-database-query).

## Configuration

### Configuring the Datadog Agent connection

The tracer sends data to a Datadog Agent. By default, it connects to `http://localhost:8126`. Use these environment variables to configure the connection:

| Environment variable | Description | Default |
|---------------------|-------------|---------|
| `DD_AGENT_HOST` | Hostname or IP where the Agent is listening | `localhost` |
| `DD_TRACE_AGENT_PORT` | TCP port where the Agent is listening | `8126` |
| `DD_TRACE_AGENT_URL` | Full URL for the Agent (overrides host and port). Supports `http://`, `https://`, and `unix://` schemes | `http://localhost:8126` |

#### Docker: connect to Agent in a sidecar container

When running in Docker with the Agent as a sidecar container, point the tracer to the Agent container:

```bash
# docker-compose.yml
services:
  my-app:
    environment:
      - DD_AGENT_HOST=datadog-agent
      - DD_TRACE_AGENT_PORT=8126
      - CORECLR_ENABLE_PROFILING=1
      - CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
      - CORECLR_PROFILER_PATH=/opt/datadog/Datadog.Trace.ClrProfiler.Native.so
      - DD_DOTNET_TRACER_HOME=/opt/datadog

  datadog-agent:
    image: datadog/agent:latest
    environment:
      - DD_API_KEY=${DD_API_KEY}
      - DD_APM_ENABLED=true
      - DD_APM_NON_LOCAL_TRAFFIC=true
    ports:
      - "8126:8126"
```

#### Kubernetes: connect to Agent DaemonSet

When the Agent runs as a DaemonSet, configure the tracer to connect via the host IP:

```yaml
# Kubernetes Pod spec
env:
  - name: DD_AGENT_HOST
    valueFrom:
      fieldRef:
        fieldPath: status.hostIP
  - name: DD_TRACE_AGENT_PORT
    value: "8126"
  - name: CORECLR_ENABLE_PROFILING
    value: "1"
  - name: CORECLR_PROFILER
    value: "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
  - name: CORECLR_PROFILER_PATH
    value: "/opt/datadog/Datadog.Trace.ClrProfiler.Native.so"
  - name: DD_DOTNET_TRACER_HOME
    value: "/opt/datadog"
```

#### Kubernetes: connect to Agent sidecar via Unix Domain Socket

For Agent sidecars in the same pod, use a Unix Domain Socket for lower latency:

```yaml
# Kubernetes Pod spec with UDS
env:
  - name: DD_TRACE_AGENT_URL
    value: "unix:///var/run/datadog/apm.socket"
  - name: CORECLR_ENABLE_PROFILING
    value: "1"
  - name: CORECLR_PROFILER
    value: "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
  - name: CORECLR_PROFILER_PATH
    value: "/opt/datadog/Datadog.Trace.ClrProfiler.Native.so"
  - name: DD_DOTNET_TRACER_HOME
    value: "/opt/datadog"
volumeMounts:
  - name: apmsocket
    mountPath: /var/run/datadog
volumes:
  - name: apmsocket
    emptyDir: {}
```

> **Note**: Unix Domain Sockets require .NET Core 3.1 or later.

#### Code-based configuration

You can also configure the Agent connection in code:

```csharp
using Datadog.Trace;
using Datadog.Trace.Configuration;

var settings = TracerSettings.FromDefaultSources();
settings.Environment = "production";
settings.ServiceName = "my-web-app";
settings.ServiceVersion = "1.0.0";
Tracer.Configure(settings);
```

> :warning: Settings must be set on `TracerSettings` **before** calling `Tracer.Configure()`. Changes made after configuration are ignored.

### Sampling configuration

Sampling controls which traces are sent to Datadog. Use these environment variables to configure sampling rules:

| Environment variable | Description | Default |
|---------------------|-------------|---------|
| `DD_TRACE_SAMPLE_RATE` | Global sampling rate for all traces (0.0 to 1.0) | `null` (Agent-based sampling) |
| `DD_TRACE_SAMPLING_RULES` | JSON array of rules to apply different rates based on service and operation name | `null` |
| `DD_TRACE_RATE_LIMIT` | Maximum number of traces per second to keep after sampling rules | `100` |

#### Global sampling rate

Set a blanket sampling rate for all traces:

```bash
# Sample 50% of all traces
DD_TRACE_SAMPLE_RATE=0.5
```

#### Custom sampling rules

Use `DD_TRACE_SAMPLING_RULES` to apply different rates based on service name and operation name. Rules are evaluated in order; the first match wins:

```bash
# Sample 100% of traces from the "payment" service, 10% of everything else
DD_TRACE_SAMPLING_RULES='[{"sample_rate":1.0, "service":"payment"}, {"sample_rate":0.1}]'
```

```bash
# Keep all errors (100%) while sampling only 10% of successful requests
# Combine service-specific rules with a low default rate:
DD_TRACE_SAMPLING_RULES='[{"sample_rate":1.0, "service":"payment"}, {"sample_rate":1.0, "service":"auth"}, {"sample_rate":0.1}]'
DD_TRACE_RATE_LIMIT=1000
```

```bash
# Sample by operation name: 100% of SQL queries, 20% of HTTP requests, 10% default
DD_TRACE_SAMPLING_RULES='[{"sample_rate":1.0, "name":"sql.query"}, {"sample_rate":0.2, "name":"http.request"}, {"sample_rate":0.1}]'
```

Each rule can include:
- `sample_rate` (required): decimal from 0.0 to 1.0
- `service` (optional): regex pattern to match the service name
- `name` (optional): regex pattern to match the operation name

#### Code-based sampling configuration

You can also configure sampling in code:

```csharp
using Datadog.Trace;
using Datadog.Trace.Configuration;

var settings = TracerSettings.FromDefaultSources();

// Set a global sampling rate (50%)
settings.GlobalSamplingRate = 0.5;

// Or set custom sampling rules (JSON format)
settings.CustomSamplingRules =
    "[{\"sample_rate\":1.0, \"service\":\"payment\"}, {\"sample_rate\":0.1}]";

// Limit the number of traces kept per second
settings.MaxTracesSubmittedPerSecond = 200;

Tracer.Configure(settings);
```

#### Manual sampling priority

You can override the sampling decision for individual traces in code. See the [manual sampling control](../docs/Datadog.Trace/README.md#manual-sampling-control) section in the NuGet package documentation.

### Log injection and trace correlation

The tracer can automatically inject trace and span IDs into your application logs, enabling you to correlate logs with traces in Datadog.

#### Enable automatic log injection

Set the `DD_LOGS_INJECTION` environment variable (enabled by default starting in v3.24.0):

```bash
DD_LOGS_INJECTION=true
```

Or enable it in code:

```csharp
using Datadog.Trace.Configuration;

var settings = TracerSettings.FromDefaultSources();
settings.LogsInjectionEnabled = true;
Tracer.Configure(settings);
```

When enabled, the tracer automatically adds the following properties to your log entries: `dd.env`, `dd.service`, `dd.version`, `dd.trace_id`, and `dd.span_id`.

#### Supported logging frameworks

Automatic injection works with:
- **Serilog** 1.4+
- **NLog** 4.0+
- **log4net** 1.0+
- **Microsoft.Extensions.Logging** 2.0+

Ensure your logs are formatted as JSON for automatic log collection, or configure [custom parsing rules](https://docs.datadoghq.com/logs/log_configuration/parsing/).

#### Serilog configuration

```csharp
// Serilog — automatic injection works out of the box with JSON formatting.
// Just ensure DD_LOGS_INJECTION=true and output as JSON:
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

// When a trace is active, log entries automatically include dd.trace_id and dd.span_id:
Log.Information("Processing order {OrderId}", orderId);
// Output: { "message": "Processing order 123", "dd.trace_id": "456...", "dd.span_id": "789...", ... }
```

#### NLog configuration (5.0+)

```xml
<!-- NLog — include scope properties in JSON output -->
<targets>
  <target xsi:type="File" name="jsonFile" fileName="app.log">
    <layout xsi:type="JsonLayout" includeScopeProperties="true">
      <attribute name="time" layout="${longdate}" />
      <attribute name="level" layout="${level:upperCase=true}" />
      <attribute name="message" layout="${message}" />
    </layout>
  </target>
</targets>
```

#### log4net configuration

```xml
<!-- log4net — include Datadog properties in JSON output -->
<appender name="JsonAppender" type="log4net.Appender.FileAppender">
  <layout type="log4net.Layout.SerializedLayout, log4net.Ext.Json">
    <member value="dd.env" />
    <member value="dd.service" />
    <member value="dd.version" />
    <member value="dd.trace_id" />
    <member value="dd.span_id" />
  </layout>
</appender>
```

#### Manual log injection

If you need to inject trace identifiers manually (for example, with an unsupported logger), use the `CorrelationIdentifier` API:

```csharp
using Datadog.Trace;
using Microsoft.Extensions.Logging;

ILogger _logger;

using (_logger.BeginScope(new Dictionary<string, object>
{
    { "dd.env", CorrelationIdentifier.Env },
    { "dd.service", CorrelationIdentifier.Service },
    { "dd.version", CorrelationIdentifier.Version },
    { "dd.trace_id", CorrelationIdentifier.TraceId.ToString() },
    { "dd.span_id", CorrelationIdentifier.SpanId.ToString() },
}))
{
    _logger.LogInformation("Processing order {OrderId}", orderId);
}
```

### Trace context propagation

The tracer automatically propagates distributed trace context across service boundaries using HTTP headers. By default, it injects and extracts the following header formats: `datadog`, `tracecontext` (W3C), and `baggage`.

| Environment variable | Description | Default |
|---------------------|-------------|---------|
| `DD_TRACE_PROPAGATION_STYLE` | Comma-separated list of header formats for both injection and extraction | `datadog,tracecontext,baggage` |
| `DD_TRACE_PROPAGATION_STYLE_INJECT` | Header formats to inject | `datadog,tracecontext,baggage` |
| `DD_TRACE_PROPAGATION_STYLE_EXTRACT` | Header formats to extract | `datadog,tracecontext,baggage` |

Available formats: `datadog`, `tracecontext` (W3C), `b3multi` (B3 multi-header), `b3` (B3 single-header), `baggage`.

For custom propagation with unsupported libraries (message queues, etc.), see the [trace context propagation](../docs/Datadog.Trace/README.md#trace-context-propagation-for-unsupported-libraries) section in the NuGet package documentation.

## Development

You can develop the tracer on various environments.

### Windows

#### Minimum requirements

- [Visual Studio 2022 (v17)](https://visualstudio.microsoft.com/downloads/) or newer
  - Workloads
    - Desktop development with C++
    - .NET desktop development
    - Optional: ASP.NET and web development (to build samples)
  - Individual components
    - When opening a solution, Visual Studio will prompt you to install any missing components.
      The prompt will appear in the "Solution Explorer". A list of all recommended components can be found in our [.vsconfig](../.vsconfig)-file.
- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
  - Optional: [.NET 7.0 x86 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) to run 32-bit tests locally
- Optional: ASP.NET Core Runtimes to run tests locally
  - [ASP.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet/2.1)
  - [ASP.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet/3.0)
  - [ASP.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet/3.1)
  - [ASP.NET Core 5.0](https://dotnet.microsoft.com/download/dotnet/5.0)
  - [ASP.NET Core 6.0](https://dotnet.microsoft.com/download/dotnet/6.0)
- Optional: [nuget.exe CLI](https://www.nuget.org/downloads) v5.3 or newer
- Optional: [WiX Toolset 3.11.1](http://wixtoolset.org/releases/) or newer to build Windows installer (msi)
  - [WiX Toolset Visual Studio Extension](https://wixtoolset.org/releases/) to build installer from Visual Studio
- Optional: [Docker for Windows](https://docs.docker.com/docker-for-windows/) to build Linux binaries and run integration tests on Linux containers.
  - Requires Windows 10 (1607 Anniversary Update, Build 14393 or newer)

Microsoft provides [evaluation developer VMs](https://developer.microsoft.com/en-us/windows/downloads/virtual-machines) with Windows and Visual Studio pre-installed.

#### Building from a command line

This repository uses [Nuke](https://nuke.build/) for build automation. To see a list of possible targets run:

```cmd
.\build.cmd --help
```

For example:

```powershell
# Clean and build the main tracer project
.\build.cmd Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
.\build.cmd BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
.\build.cmd PackageTracerHome

# Build and run integration tests. Requires BuildTracerHome to have previously been run
.\build.cmd BuildAndRunWindowsIntegrationTests
```

### Dev Containers

#### VS Code

##### Prerequisites

- Install the [Dev Containers Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) in VS Code.

##### Steps

1. Open a local VS Code window on the cloned repository.
2. Open the command palette (`Ctrl+Shift+P` or `Cmd+Shift+P` on macOS) and select **"Dev Containers: Reopen in Container"**.
3. Choose the **Tracer**.
4. VS Code will open a new window connected to the selected container.
5. Open the command palette again and select **.NET: Open Solution**. Choose the `Datadog.Trace.Minimal.slnf` solution file. Read more at [Project Management](https://code.visualstudio.com/docs/csharp/project-management).
6. You can now build and run the tracer in the devcontainer.
7. [Optional] Open the command palette, select `Tasks: Run Build Task`, `Tracer: Build on {OS} with Target` and choose the target you want to build.

#### Rider

##### Prerequisites

- Ensure the **Dev Containers** plugin is enabled (it comes bundled with Rider).

##### Steps

1. Open **Rider**, select **Remote Development**, then choose **Dev Containers**.
2. Since we want to use the **Dev Container** from the repository, select **"From Local Project"** and choose `.devcontainer/devcontainer.json`.
3. In the **Select a Solution to Open** window, pick `Datadog.Trace.Minimal.slnf`.
4. You can now build and run the tracer inside the **Dev Container**.

#### Tips

- Currently, the devcontainer is configured to use `debian.dockerfile`, but you can change it to either a local Dockerfile or a remote image as per your requirements.
- Building Tracer can be resource-intensive and may even run out of memory (OOM) in some cases. If you encounter the error `MSB6006: "csc.dll" exited with code 137.`, increase the memory allocated to the devcontainer (16GB is recommended).
- `Datadog.Trace.Minimal.slnf` is a minimal solution file that includes all the projects required to build the tracer. You can open other solutions as well, but they may not be fully supported in the devcontainer.

### Linux

The recommended approach for Linux is to build using Docker. You can use this approach for both Windows and Linux hosts. The _build_in_docker.sh_ script automates building a Docker image with the required dependencies, and running the specified Nuke targets. For example, on Linux:

```bash
# Clean and build the main tracer project
./build_in_docker.sh Clean BuildTracerHome

# Build and run managed unit tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunManagedUnitTests

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./build_in_docker.sh BuildAndRunLinuxIntegrationTests
```

Alternatively, on Windows:
```powershell
./build_in_docker.ps1 BuildTracerHome BuildAndRunLinuxIntegrationTests
```

### macOS

You can use Rider and CLion, or Visual Studio Code to develop on macOS. When asked to select a solution file select `Datadog.Trace.OSX.slnf`. If using CLion for the native code make sure to select "Let CMake decide" for the generator.
Building and testing can be done through the following Nuke targets:

### Setup

- Install [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet)

```bash
# Install cmake
brew install cmake
```

### Running tests

```bash
# Clean and build the main tracer project
./build.sh Clean BuildTracerHome

# Build and run managed and native unit tests. Requires BuildTracerHome to have previously been run
./build.sh BuildAndRunManagedUnitTests BuildAndRunNativeUnitTests

# Build NuGet packages and MSIs. Requires BuildTracerHome to have previously been run
./build.sh PackageTracerHome

# Start IntergrationTests dependencies, but only for a specific test
docker-compose up rabbitmq_osx_arm64

# Start IntegrationTests dependencies.
docker-compose up StartDependencies.OSXARM64

# Build and run integration tests. Requires BuildTracerHome to have previously been run
./build.sh BuildAndRunOsxIntegrationTests

# Build and run integration tests filtering on one framework, one set of tests and a sample app.
./build.sh BuildAndRunOsxIntegrationTests --framework "net6.0" --filter "Datadog.Trace.ClrProfiler.IntegrationTests.RabbitMQTests" --SampleName "Samples.Rabbit"

# Stop IntegrationTests dependencies.
docker-compose down
```

Troubleshooting tips for build errors:
 * Try deleting the `cmake-build-debug` and `obj_*` directories.
 * Verify your xcode developer tools installation with `xcode-select --install`. You may need to repeat this process after an operating system update.


## Additional Technical Documentation

* [Implementing an automatic instrumentation](../docs/development/AutomaticInstrumentation.md)
* [Duck typing: usages, best practices, and benchmarks](../docs/development/DuckTyping.md)
* [Datadog.Trace NuGet package README](../docs/Datadog.Trace/README.md)

## Further Reading

Datadog APM
- [Datadog APM](https://docs.datadoghq.com/tracing/)
- [Datadog APM - Tracing .NET Core Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core)
- [Datadog APM - Tracing .NET Framework Applications](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework)

Microsoft .NET Profiling APIs
- [Profiling API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/)
- [Metadata API](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/)
- [The Book of the Runtime - Profiling](https://github.com/dotnet/coreclr/blob/master/Documentation/botr/profiling.md)

OpenTracing
- [OpenTracing documentation](https://github.com/opentracing/opentracing-csharp)
- [OpenTracing terminology](https://github.com/opentracing/specification/blob/master/specification.md)
