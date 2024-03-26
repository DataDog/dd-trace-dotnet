# C# Log Collection and Connecting Logs and Traces <!-- omit from toc -->

- [Introduction](#introduction)
- [Prerequisites](#prerequisites)
  - [File-tail Log Collection](#file-tail-log-collection)
  - [Agentless Log Collection](#agentless-log-collection)
  - [Connecting Logs to Traces](#connecting-logs-to-traces)
- [Running the Samples](#running-the-samples)
  - [Serilog](#serilog)
    - [Serilog Configuration](#serilog-configuration)
    - [Serilog Expected Output](#serilog-expected-output)
    - [Running the Serilog Sample](#running-the-serilog-sample)
  - [Log4Net](#log4net)
    - [Log4Net Configuration](#log4net-configuration)
    - [Running the Log4Net Sample](#running-the-log4net-sample)
    - [Log4Net Expected Output](#log4net-expected-output)
  - [NLog](#nlog)
    - [Configuration](#configuration)
    - [Running the NLog Samples](#running-the-nlog-samples)
    - [NLog Expected Output](#nlog-expected-output)
  - [Microsoft.Extensions.Logging](#microsoftextensionslogging)
    - [Microsoft.Extensions.Logging Configuration](#microsoftextensionslogging-configuration)
    - [Running the Microsoft.Extensions.Logging Sample](#running-the-microsoftextensionslogging-sample)
    - [Expected Output for Microsoft.Extensions.Logging Sample](#expected-output-for-microsoftextensionslogging-sample)

## Introduction

The following samples demonstrate the C# Log Collection feature along with connecting logs to traces automatically.

For additional documentation on these features and how to enable them in your environments, refer to:

- [C# File-tail Log Collection](https://docs.datadoghq.com/logs/log_collection/csharp/?tab=serilog#file-tail-logging-with-the-datadog-agent)
- [C# Agentless Log Collection](https://docs.datadoghq.com/logs/log_collection/csharp/?tab=serilog#agentless-logging-with-apm)
- [C# Connecting Logs to Traces](https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=serilog)

> If there is a logging layout that you would like to see documented here, please feel free to reach out with an issue or contribution!

## Prerequisites

- Powershell 7+ to run `BuildAndRunSample.ps1`
- `dotnet` command line tool is installed
- .NET 7.0 or .NET Framework 4.6.2
- [Datadog Agent](https://docs.datadoghq.com/agent/) is installed
  - Note that this is **not** a requirement for the Agentless Log Collection
- Any of the following operating systems:
  - Windows 32-bit
  - Windows 64-bit
  - Linux 64-bit

The `BuildAndRunSample.ps1` script will set the necessary environment variables for the given sample automatically.
However, there may be some additional setup required to try out each of the features.

All samples use the [Datadog.Trace.Bundle](https://www.nuget.org/packages/Datadog.Trace.Bundle#readme-body-tab) NuGet package for automatic and manual instrumentation.
**However**, you can setup a project to use the [Datadog.Trace](https://www.nuget.org/packages/Datadog.Trace) NuGet package for manual instrumentation and configure automatic instrumentation separately.

Please refer to our [README](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/Datadog.Trace.Bundle/README.md#should-i-install-this-package) on when to use `Datadog.Trace` versus `Datadog.Trace.Bundle` and our [official documentation](https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core/?tab=containers) on ways to install the Datadog Tracer.

### File-tail Log Collection

> Further documentation and setup information about File-tail Log Collection can be found [here](https://docs.datadoghq.com/logs/log_collection/csharp/?tab=serilog#file-tail-logging-with-the-datadog-agent)

File-tail log collection requires the following:

- [Configured the Datadog Agent](https://docs.datadoghq.com/logs/log_collection/csharp/?tab=serilog#configure-the-datadog-agent) for logs collection which covers:
  - Enabling [log collection](https://docs.datadoghq.com/agent/logs/?tab=tailfiles#activate-log-collection) for the agent
  - Configuring [custom log collection](https://docs.datadoghq.com/agent/logs/?tab=tailfiles#custom-log-collection) for the agent
- The `path:` value will change based on which sample is being run. In general log files are output in the same directory as the built project for these samples
  - This should be an *absolute* path to the log file
  - Example *relative* output location of log file for the Log4Net's sample: `\Log4NetExample\bin\Debug\net462\win-x86\log-log4net-jsonFile-allProperties.log`

### Agentless Log Collection

> Further documentation and setup information about Agentless Log Collection can be found [here](https://docs.datadoghq.com/logs/log_collection/csharp/?tab=serilog#agentless-logging-with-apm)

Agentless logging requires two additional parameters to be passed into the script:

- The `-Agentless` switch and
- Your Datadog `-ApiKey`

### Connecting Logs to Traces

> Further documentation and setup information about Connecting Logs and Traces can be found [here](https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=serilog)

The provided samples automatically take care of all necessary requirements for connecting traces to logs besides the initial [Prerequisites](#prerequisites).

## Running the Samples

A sample is provided for each of the logging libraries that the .NET Tracer supports: `Serilog`, `Log4Net`, `NLog`, and `Microsoft.Extensions.Logging`.

If you are running into issues using the `-EnableDebug` switch will enable the .NET Tracer Debug logs, which may help identifying configuration issues.

> Assumed that the active working directory is `samples\AutomaticTraceIdInjection\`

The script provides several parameters to run different samples:

- `-LoggingLibrary` (string) to specify which sample to build and run e.g. `Log4Net`
  - Exclude the `*Example` portion of the project name
- `-Framework` (string) either `net7.0` or `net462`
- `-Runtime` (string) either `x86` or `x64`
  - For Linux only `x64` is supported
- `-Agentless` (switch) to specify whether to enable agentless logging
- `-ApiKey` (string) your Datadog API key - only required when `-Agentless` is specified
- `-EnableDebug` (switch) to enable .NET Tracer debug logs for debugging configuration issues

Note: the operating system will be determined automatically by the script.

Example command to run the `Log4Net` example:

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Log4Net -Framework net7.0 -Runtime x86
```

### Serilog

Located within the [SerilogExample](SerilogExample) directory.

#### Serilog Configuration

Refer to the [Program.cs](SerilogExample/Program.cs) file to see how Serilog is configured.
Layouts configured in the sample that produce three log files in the same directory that the project is built to:

- `CompactJsonFormatter` that requires no additional configuration in code
  - `log-Serilog-compactJsonFile-allProperties.log`
- `JsonFormatter` that requires no additional configuration in code
  - `log-Serilog-jsonFile-allProperties.log`
- A simple `MessageTemplate` (non-JSON) that requires additional configuration in code to emit the necessary properties
  - `log-Serilog-textFile.log`

#### Serilog Expected Output

This sample will create three log messages along with a single trace.

- A log message before the trace
- A trace `"SerilogExample - Main()"`
- A log message during the trace
- A log message after the trace

When logs are connected to traces the `"SerilogExample - Main()"` trace will have a log message connected to it that was logged during the the trace.

#### Running the Serilog Sample

> Running with file-tail log collection on Windows x86 .NET 7.0 (this requires that the agent is configured to point to one of the above log files)

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Serilog -Framework net7.0 -Runtime x86
```

> Running with Agentless logging on x64 Windows .NET Framework 4.6.2

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Serilog -Framework net462 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

> Running with Agentless logging on x64 .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Serilog -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

### Log4Net

Located within the [Log4NetExample](Log4NetExample) directory.

#### Log4Net Configuration

Refer to the `log4net.config` to see the specific logging configuration(s) being used.
Layouts configured in the sample that produce three log files in the same directory that the project is built to:

- JSON format all properties: `SerializedLayout` (from the `log4net.Ext.Json` NuGet package)
  - `log-log4net-jsonFile-allProperties.log`
- JSON format with specific properties
  - `log-log4net-jsonFile-explicitProperties.log`
- Raw format: `PatternLayout` (requires a custom Datadog Log Pipeline for processing)
  - `log-log4net-textFile.log`

#### Running the Log4Net Sample

> Running with file-tail log collection on Windows x86 .NET 7.0 (this requires that the agent is configured to point to one of the above log files)

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Log4Net -Framework net7.0 -Runtime x86
```

> Running with Agentless logging on x64 Windows .NET Framework 4.6.2

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Log4Net -Framework net462 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

> Running with Agentless logging on x64 .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary Log4Net -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

#### Log4Net Expected Output

The Log4Net sample will create three log messages and one trace:

- A log message before the trace
- A trace `"Log4NetExample - Main()"`
- A log message during the trace
- A log message after the trace

When logs are connected to traces the `"Log4NetExample - Main()"` trace will have a log message connected to it that was logged during the the trace.

### NLog

Several samples, each for a specific version of NLog are provided:

- NLog v4.0 within the [NLog40Example](NLog40Example) directory
  - Note that NLog v4.0's sample only supports .NET Framework 4.6.2
- NLog v4.5 within the [NLog45Example](NLog45Example) directory
- NLog v4.6 within the [NLog46Example](NLog46Example) directory

#### Configuration

Refer to the `NLog.config` within each `NLog` example project to see the specific logging configuration(s) being used.
Example for `NLog46`: layouts configured in the sample produce three log files in the same directory that the project is built to:

- JSON format all properties without needing to extract Datadog properties (`includeMdlc="true"`)
  - `log-NLog46-jsonFile-includeMdlc-true.log`
- JSON format while needing to specify the Datadog properties to extract (`includeMdlc="false"`)
  - `log-NLog46-jsonFile-includeMdlc-false.log`
- Custom format - Datadog properties must be extracted individually using `${mdlc:item=String}`
  - `log-NLog46-textFile.log`

#### Running the NLog Samples

> Running with file-tail log collection on Windows x86 .NET Framework 4.6.2 (this requires that the agent is configured to point to one of the above log files)

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary NLog40 -Framework net462 -Runtime x86
```

> Running with Agentless logging on x64 Windows .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary NLog45 -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

> Running with Agentless logging on x64 .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary NLog46 -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

#### NLog Expected Output

The NLog sample will create three log messages and one trace:

- A log message before the trace
- A trace `"NLog46Example - Main()"` (name changes based on the version of the NLog sample run)
- A log message during the trace
- A log message after the trace

When logs are connected to traces the `"NLog46Example - Main()"` trace will have a log message connected to it that was logged during the the trace.

### Microsoft.Extensions.Logging

Located within the [MicrosoftExtensionsExample](MicrosoftExtensionsExample) directory.

Note that `Microsoft.Extensions.Logging` doesn't have an official logger that can write to a file and operates slightly different compared to the other samples.

#### Microsoft.Extensions.Logging Configuration

The `Microsoft.Extensions.Logging` sample is configured in two ways within its `Program.cs` file:

- For .NET 7.0 configuration it utilizes the `NetEscapades.Extensions.Logging.RollingFile` NuGet to write to a rolling JSON-formatted log file
- For .NET Framework 4.6.2 it utilizes `Serilog` to write to a JSON-formatted log file.

What isn't shown in this sample is how to manually inject Trace IDs within the logs - for this please refer to our official documentation on that [here](https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=microsoftextensionslogging#manual-injection).

#### Running the Microsoft.Extensions.Logging Sample

> Note that this sample runs in a loop and needs to be terminated by the user (e.g. `Ctrl` + `c`)

> Running with file-tail log collection on Windows x86 .NET Framework 4.6.2 (this requires that the agent is configured to point to one of the above log files)

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary MicrosoftExtensions -Framework net462 -Runtime x86
```

> Running with Agentless logging on x64 Windows .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary MicrosoftExtensions -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

> Running with Agentless logging on x64 .NET 7.0

```powershell
pwsh BuildAndRunSample.ps1 -LoggingLibrary MicrosoftExtensions -Framework net7.0 -Runtime x64 -Agentless -ApiKey YOUR_API_KEY_HERE
```

#### Expected Output for Microsoft.Extensions.Logging Sample

The MicrosoftExtensions sample will create *at least*: four log messages and one trace with two spans:

- A log message before the trace
- A trace `"MicrosoftExtensions - Main()"`
- A log message during the trace
- A child span within `"MicrosoftExtensions - Main()"` called `"Microsoft.Extensions.Example - Worker.ExecuteAsync()"`
- A log message during the child span
- A log message after the trace

When logs are connected to traces the `"MicrosoftExtensions - Main()"` span along with the child span will have a log message connected to them that was logged when the span was active.
