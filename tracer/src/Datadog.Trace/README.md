# Datadog.Trace

Core managed tracer library implementing Datadog APM instrumentation for .NET.

## Purpose

The heart of the .NET tracer, providing:
- **Auto-instrumentation**: CallTarget system for automatic library instrumentation
- **APM**: Distributed tracing, spans, context propagation
- **Application Security**: AppSec (WAF/RASP), IAST
- **CI Visibility**: Test/session/module tracing
- **Continuous Profiler**: Coordination hooks for CPU/wall profiler
- **Data Streams Monitoring**: Kafka/messaging pathway tracking
- **Database Monitoring**: DBM query samples and metrics
- **Dynamic Instrumentation**: Live debugger probes and snapshots
- **Agent Communication**: HTTP/Unix socket transports, trace serialization
- **Telemetry**: Product usage and metrics collection

## Key Functionalit

- **ClrProfiler/AutoInstrumentation**: 100+ integrations for popular libraries (HTTP, databases, messaging, etc.)
- **Agent**: Transport, serialization (MessagePack), discovery, sampling
- **Configuration**: Environment variables, JSON, remote configuration (RCM)
- **Propagators**: Context injection/extraction (Datadog, W3C, B3)
- **Telemetry**: Product telemetry emission to agent
- **Vendors**: Vendored dependencies (Newtonsoft.Json, Serilog, MessagePack, StatsdClient, dnlib)

See `AGENTS.md` for detailed module structure.

## Dependencies

Project references:
- `Datadog.Trace.SourceGenerators` (analyzer, compile-time only)

## Dependents

- `Datadog.Trace.BenchmarkDotNet` - BenchmarkDotNet exporter
- `Datadog.Trace.Coverage.collector` - Code coverage collector
- `Datadog.Trace.MSBuild` - MSBuild logger
- `Datadog.Trace.Tools.Runner` (dd-trace) - CI Visibility runner

## Artifacts

### Library
- **Name**: `Datadog.Trace.dll`
- **Target Frameworks**: net461, netstandard2.0, netcoreapp3.1, net6.0
- **Deployment**: Shipped in monitoring-home directory structure
- **Type**: Core tracer library loaded by `Datadog.Trace.ClrProfiler.Managed.Loader`

**Note**: Not directly published as NuGet package. Customers use `Datadog.Trace` NuGet package which is built from `Datadog.Trace.Manual` project.
