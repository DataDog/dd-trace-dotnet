# Datadog APM .NET Client Libraries

<img align="right" style="margin-left:10px" src="https://user-images.githubusercontent.com/22597395/202840005-3cc7ffd9-4a22-4c59-99ab-293d6f616a19.svg" alt="bits dotnet" width="200px"/>

This repository contains the sources for the client-side components of the Datadog product suite for Application Telemetry Collection and Application Performance Monitoring for .NET Applications.

**[Datadog .NET Tracer](https://github.com/DataDog/dd-trace-dotnet/tree/master/tracer)**: A set of .NET libraries that let you trace any piece of your .NET code. It automatically instruments supported libraries out-of-the-box and also supports custom instrumentation to instrument your own code.

This library powers [Distributed Tracing](https://docs.datadoghq.com/tracing/),
[Application Security Management](https://docs.datadoghq.com/tracing/profiler/connect_traces_and_profiles/),
[Continuous Integration Visibility](https://docs.datadoghq.com/continuous_integration/) and more.

**[Datadog .NET Continuous Profiler](https://github.com/DataDog/dd-trace-dotnet/tree/master/profiler)**: Libraries that automatically profile your application. [Documentation](https://docs.datadoghq.com/tracing/profiler/).


## Downloads

| Package                      | Download                                                                                                                                                  |
|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| Windows and Linux Installers | [![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/DataDog/dd-trace-dotnet)](https://github.com/DataDog/dd-trace-dotnet/releases)                                                                                       |
| `Datadog.Trace`              | [![Datadog.Trace](https://img.shields.io/nuget/vpre/Datadog.Trace.svg)](https://www.nuget.org/packages/Datadog.Trace)                                     |
| `Datadog.Trace.OpenTracing`  | [![Datadog.Trace.OpenTracing](https://img.shields.io/nuget/vpre/Datadog.Trace.OpenTracing.svg)](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) |

## Build status

Build status on `master`: [![Build](https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/status/consolidated-pipeline?branchName=master&stageName=build_windows_tracer)](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/latest?definitionId=54&branchName=master)

## Copyright

Copyright (c) 2017 Datadog
[https://www.datadoghq.com](https://www.datadoghq.com/)

## License

See [license information](../LICENSE).

## Contact us

### Security Vulnerabilities

If you have found a security issue, please contact the security team directly at [security@datadoghq.com](mailto:security@datadoghq.com).

### Other feedback

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
