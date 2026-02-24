# Datadog.Trace.BenchmarkDotNet

BenchmarkDotNet exporter for Datadog CI Visibility.

## Purpose

Integrates BenchmarkDotNet with Datadog CI Visibility to:
- Report benchmark results to Datadog
- Track performance trends over time
- Integrate benchmarks into CI pipeline visibility
- Bundle tracer components for benchmark execution

## Key Functionality

- **BenchmarkDotNet exporter**: IExporter implementation for Datadog
- **CI Visibility integration**: Report benchmarks as CI test spans
- **Tracer bundling**: Includes monitoring-home assets in content directory

## Dependencies

Project references:
- `Datadog.Trace` - Core tracer library

Package references:
- `BenchmarkDotNet` - Benchmark framework

## Dependents

None - consumed as NuGet package by benchmark projects.

## Artifacts

### NuGet Package
- **Package**: `Datadog.Trace.BenchmarkDotNet`
- **Target Frameworks**: net461, netstandard2.0, netcoreapp3.1, net6.0
- **Content**: Monitoring-home directory with tracer assets
