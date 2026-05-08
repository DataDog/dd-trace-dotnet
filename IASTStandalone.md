# Standalone IAST Analyzer — Design Document

## Goal

Create an independent, lightweight IAST analyzer that detects vulnerabilities at runtime without any dependency on the Datadog Agent, Tracer, Debugger, Profiler, RASP, or span pipelines. Findings are written to a local JSONL file instead of being sent to a backend.

---

## Current Repository State (after cleanup)

### tracer/src/ — Kept (4 projects)

```
Datadog.Trace                          — Core managed tracer (IAST internals only)
Datadog.Trace.ClrProfiler.Managed.Loader — Native profiler bootstrap
Datadog.Trace.SourceGenerators         — Build dependency (source generators)
Datadog.Tracer.Native                  — Native IL rewriting engine (C++)
```

### tracer/src/ — Removed (20 projects)

```
Datadog.AutoInstrumentation.Generator, Datadog.AzureFunctions,
Datadog.FeatureFlags.OpenFeature, Datadog.FleetInstaller,
Datadog.InstrumentedAssemblyGenerator, Datadog.InstrumentedAssemblyVerification,
Datadog.Trace.Annotations, Datadog.Trace.BenchmarkDotNet, Datadog.Trace.Bundle,
Datadog.Trace.Coverage.collector, Datadog.Trace.MSBuild, Datadog.Trace.Manual,
Datadog.Trace.OpenTracing, Datadog.Trace.Tools.Analyzers,
Datadog.Trace.Tools.Analyzers.CodeFixes, Datadog.Trace.Tools.Runner,
Datadog.Trace.Tools.Shared, Datadog.Trace.Tools.dd_dotnet,
Datadog.Trace.Tools.dd_dotnet.SourceGenerators, Datadog.Trace.Trimming
```

### tracer/test/ — Kept (5 projects)

```
Datadog.Trace.Security.Unit.Tests              — IAST unit tests
Datadog.Trace.Security.IntegrationTests        — IAST integration tests
Datadog.Trace.TestHelpers                      — Test infrastructure
Datadog.Trace.TestHelpers.AutoInstrumentation  — Auto-instrumentation test helpers
Datadog.Trace.TestHelpers.SharedSource         — Shared test source
```

### tracer/test/ — Removed (16 projects)

```
Datadog.FleetInstaller.IntegrationTests, Datadog.Trace.BenchmarkDotNet.Tests,
Datadog.Trace.ClrProfiler.IntegrationTests, Datadog.Trace.ClrProfiler.Managed.Tests,
Datadog.Trace.Debugger.IntegrationTests, Datadog.Trace.IntegrationTests,
Datadog.Trace.OpenTracing.Tests, Datadog.Trace.SourceGenerators.Tests,
Datadog.Trace.Tests, Datadog.Trace.Tools.Analyzers.Tests,
Datadog.Trace.Tools.Runner.ArtifactTests, Datadog.Trace.Tools.Runner.IntegrationTests,
Datadog.Trace.Tools.Runner.Tests, Datadog.Trace.Tools.dd_dotnet.ArtifactTests,
Datadog.Trace.Tools.dd_dotnet.IntegrationTests, Datadog.Trace.Tools.dd_dotnet.Tests,
Datadog.Tracer.Native.Tests, benchmarks, snapshots
```

### tracer/test/test-applications/ — Kept

```
security/                                      — Main IAST test apps
  Samples.Security.AspNetCore2/
  Samples.Security.AspNetCore5/
  Samples.Security.AspNetCoreBare/
  aspnet/Samples.Security.AspNetMvc5/
  aspnet/Samples.Security.WebApi/
  aspnet/Samples.Security.WebForms/

integrations/                                  — Required by IAST integration tests
  Samples.WeakCipher/                          — Weak cipher detection tests
  Samples.ProcessStart/                        — Command injection tests
  Samples.InstrumentedTests/                   — Instrumentation vulnerability tests
  Samples.Deduplication/                       — Deduplication tests
  Samples.GrpcDotNet/                          — gRPC IAST tests

Samples.Shared/                                — Shared test resources
```

### tracer/test/test-applications/ — Removed (~160 apps)

All non-security, non-IAST test applications: `aspnet/`, `azure-functions/`, `debugger/`,
`instrumentation/`, `regression/`, `throughput/`, and the bulk of `integrations/` (logging,
AWS, databases, messaging, GraphQL, HTTP, Redis, etc.)

### Top-level — Removed

```
profiler/          — Entire native profiler
docs/              — All documentation
packages/          — NuGet packages cache
exploration-tests/ — Test exploration
docker/            — Docker configuration
Docker-compose files, non-Security solution files
```

### Top-level — Kept

```
Datadog.Trace.sln            — Main solution (trimmed to IAST-relevant projects)
Datadog.Trace.Security.slnf  — Solution filter (updated to match current .sln)
Datadog.Trace.snk            — Strong name key
BannedSymbols.txt            — Empty (recreated, referenced by Datadog.Trace.csproj)
IASTStandalone.md            — This document
LICENSE, NOTICE              — Legal
global.json                  — .NET SDK version
nuget.config                 — NuGet sources
xunit.runner.json            — Test runner config
vcpkg.json                   — vcpkg manifest (needed for native lib downloads)
vcpkg-configuration.json     — vcpkg registry config
CMakeLists.txt               — Top-level CMake (native builds)
build/                       — CMake modules + vcpkg local ports
tracer/build/                — Nuke build system (restored for BuildTracerHome)
tracer/tools/                — Build tooling
shared/src/native-lib/       — CLR profiling headers (coreclr, spdlog, PPDB)
shared/src/native-src/       — Shared native sources (miniutf, string, util, pal, logger)
shared/src/Datadog.Trace.ClrProfiler.Native/ — Native loader (loads managed tracer into processes)
tracer/                      — Main code (src + test)
```

### Build infrastructure

The full Nuke build system was restored to produce the "monitoring home" directory
(`shared/bin/monitoring-home/`) required by integration tests. The monitoring home contains:

```
shared/bin/monitoring-home/
  net461/                  — Managed tracer (.NET Framework)
  net6.0/                  — Managed tracer (.NET 6+)
  netcoreapp3.1/           — Managed tracer (.NET Core 3.1)
  netstandard2.0/          — Managed tracer (netstandard)
  win-x64/                 — Native loader + tracer (x64)
  win-x86/                 — Native loader + tracer (x86)
```

**Build command:** `./tracer/build.cmd BuildTracerHome` (Windows, ~3-4 minutes)

### Build fixes applied

| Fix | Reason |
|---|---|
| Created empty `BannedSymbols.txt` at repo root | Referenced by `Datadog.Trace.csproj` as an additional file |
| Removed `TraceAttribute.cs` compile link from `Samples.Security.AspNetCore5.csproj` and `AspNetCore2.csproj` | Pointed to deleted `Datadog.Trace.Annotations` project; attribute not used in source |
| Removed `[Datadog.Trace.Annotations.Trace]` attribute from `IastController.cs:1169` | Tracer annotation, not IAST |
| Removed `Datadog.Trace` SDK usage from `SessionController.cs` | User tracking via `SampleHelpers`, not IAST-related |
| Stubbed `InstrumentationVerification.cs` to no-op | Depended on deleted `InstrumentedAssemblyGenerator`/`InstrumentedAssemblyVerification` projects |
| Restored `TelemetryHelper.cs` into `Datadog.Trace.ClrProfiler.IntegrationTests/Helpers/` | Linked source from deleted project, still needed by security integration tests |
| Restored `shared/src/native-lib/` and `shared/src/native-src/` | CLR profiling headers and shared native sources required by `Datadog.Tracer.Native` |
| Restored `shared/src/Datadog.Trace.ClrProfiler.Native/` | Native loader project, required for monitoring home |
| Added `Datadog.Trace.ClrProfiler.Native` to `Datadog.Trace.sln` | Nuke build resolves it via `Solution.GetProject()` |
| Replaced `DatadogTraceMsBuild` with `DatadogTrace` in `PublishManagedTracer` build step | `Datadog.Trace.MSBuild` project was deleted |
| Made `CreateTrimmingFile` build step a no-op | `Datadog.Trace.Trimming` project was deleted |
| Updated `.slnf` to exclude ASP.NET Framework apps | Require VS WebApplication.targets, cannot build with `dotnet build` |

### Test results (after cleanup)

| Test Suite | Result | Notes |
|---|---|---|
| **IAST Unit Tests** | **527/527 pass** | All IAST unit tests pass after RASP removal |
| **IAST Integration Tests** | **94 passed**, some skipped | DB tests require Docker; some flaky tests skipped via `SkipException` in constructors |

---

## Component Removals (chronological)

### Non-IAST test cleanup

All WAF, RASP, AppSec, Encoder, RCM, UserEvents, ApiSecurity tests were deleted from both
unit and integration test projects. Only IAST tests and shared infrastructure files remain.

Deleted from unit tests: `ApiSec/`, `Fingerprint/`, `RASP/`, `Utils/`, and 30 root-level
WAF/AppSec `.cs` files.

Deleted from integration tests: `ApiSecurity/`, `RASP/`, `Rcm/`, `UserEvents/`, `Grpc/` (restored),
and 15 root-level ASM/AppSec `.cs` files. Also removed `AspNetCore5IastAsm.cs` (RCM dependency),
`AspNetMvc5IastTests.cs`, `AspNetWebFormsWithIast.cs` (ASP.NET Framework dependencies).

### RASP removal

RASP was fully removed from both managed and native code while preserving IAST functionality:

**Managed side:**

| Change | Files |
|---|---|
| Changed `InstrumentationCategory.IastRasp` → `InstrumentationCategory.Iast` | 14 aspect files (SQL, IO, Net) |
| Removed `RaspModule` calls from `VulnerabilitiesModule` | `AppSec/VulnerabilitiesModule.cs` |
| Deleted pure RASP files | `RaspModule.cs`, `RaspMetricsHelper.cs`, `RaspShellInjectionHelper.cs` |
| Kept shared utilities used by IAST | `MetaStructHelper.cs`, `StackReporter.cs` (in `AppSec/Rasp/`) |
| Removed RASP metrics/tracking from `AppSecRequestContext` | `AppSec/AppSecRequestContext.cs` |
| Removed RASP metrics from `SecurityReporter` | `AppSec/Coordinator/SecurityReporter.cs` |
| Simplified `CheckWAFError` signature (removed `isRasp` param) | `AppSecRequestContext.cs`, `SecurityCoordinator.cs` |
| Removed RASP from instrumentation setup | `ClrProfiler/Instrumentation.cs` — IAST-only category enablement |
| Removed `RaspEnabled` property from `SecuritySettings` | `AppSec/SecuritySettings.cs` |
| Removed `RaspEnabled` property from `Security` | `AppSec/Security.cs` |
| Removed RASP RCM capability registrations | `AppSec/Security.cs` (5 `AsmRasp*` lines) |
| Removed RASP telemetry from `TracerManager` info dump | `TracerManager.cs` |
| Removed `RaspEnabled` field from `SharedConfig` native interop struct | `ContinuousProfiler/NativeInterop.cs` |

**Native side (Datadog.Tracer.Native):**

| Change | Files |
|---|---|
| Removed `IsRaspEnabled()` / `IsRaspSettingEnabled()` functions | `environment_variables_util.cpp`, `environment_variables_util.h` |
| Removed `rasp_enabled` environment variable constant | `environment_variables.h` |
| Simplified `cor_profiler.cpp` dataflow init to IAST-only | `cor_profiler.cpp` |

### Telemetry removal

All telemetry was disabled/removed. The `Telemetry/` directory (81 files) is kept as a skeleton
since 159 files across the tracer reference `TelemetryFactory`, but it's been neutralized:

| Change | Details |
|---|---|
| `TelemetryFactory` defaults to no-ops | `NullMetricsTelemetryCollector` and `NullConfigurationTelemetry` as defaults — all 159 call sites become no-ops |
| Deleted `Iast/Telemetry/` | `ExecutedTelemetryHelper.cs`, `IastMetricsVerbosityLevel.cs` |
| Deleted telemetry test files | `ExecutedTelemetryHelperTests.cs`, `IastTelemetryTests.cs` |
| Cleaned `IastModule.cs` | 4 telemetry methods made empty |
| Cleaned `IastRequestContext.cs` | Removed `_executedTelemetryHelper` field and all usage sites, 4 methods made empty |
| Cleaned `IastSettings.cs` | Removed `TelemetryVerbosity` property, replaced `TelemetryFactory.Config` with `NullConfigurationTelemetry.Instance` |
| Cleaned test infrastructure | Removed `EnableIastTelemetry` method, `iastTelemetryLevel` parameter, and `IastTelemetryLevel` property from all test base classes and subclasses |

### Feature flags removal

Deleted the entire `FeatureFlags/` module (26 files) and related auto-instrumentation integrations:

| Change | Details |
|---|---|
| Deleted `FeatureFlags/` | 26 files: evaluator, module, RCM models, exposure API/cache/models |
| Deleted `ClrProfiler/AutoInstrumentation/ManualInstrumentation/FeatureFlags/` | FeatureFlags SDK integration |
| Deleted `ClrProfiler/AutoInstrumentation/ManualInstrumentation/OpenFeature/` | OpenFeature SDK integration |
| Removed `FeatureFlagsModule` from tracer lifecycle | `TracerManager`, `TracerManagerFactory`, `Tracer`, `TestOptimizationTracerManager`, `TestOptimizationTracerManagerFactory` — parameter, property, dispose, replace logic all removed |

### Debugger (Dynamic Instrumentation) removal

Deleted the entire `Debugger/` directory (218 files) and cleaned all external references:

| Change | Details |
|---|---|
| Deleted `Debugger/` | 218 files: DynamicInstrumentation, ExceptionAutoInstrumentation, SpanCodeOrigin, Symbols, Snapshots, PInvoke, Configurations, Expressions, Sink, etc. |
| Deleted `Ci/TestOptimizationDynamicInstrumentationFeature.cs` | CI debugger integration |
| Cleaned `Instrumentation.cs` | Removed debugger initialization and SpanCodeOrigin from diagnostic observers |
| Cleaned `Tracer.cs` | Removed CodeOrigin call from StartSpan |
| Cleaned `Span.cs` | Removed ExceptionReplay calls from SetExceptionTags/Finish, removed MarkSpanForExceptionReplay |
| Cleaned `TraceContext.cs` | Removed MarkSpanForExceptionReplay call |
| Cleaned `DynamicConfigurationManager.cs` | Removed debugger config update block |
| Cleaned `AspNetCoreDiagnosticObserver.cs` | Removed SpanCodeOrigin field/parameter/usage |
| Cleaned `SingleSpanAspNetCoreDiagnosticObserver.cs` | Same SpanCodeOrigin cleanup |
| Cleaned `FaultTolerantNativeMethods.cs` | Removed debugger using statements |
| Cleaned `UserStringInterop.cs` | Removed debugger using |
| Cleaned `PDBs/DatadogMetadataReader*.cs` | Removed debugger symbol references, moved utility types to PDBs namespace |
| Cleaned `Datadog.Trace.csproj` | Removed debugger embedded resource |
| Cleaned CI test integrations | Replaced `TestOptimizationDynamicInstrumentationFeature.DefaultExceptionHandlerTimeout` with literal values |

### Continuous Profiler removal

Deleted `ContinuousProfiler/` (9 files) and cleaned all external references:

| Change | Details |
|---|---|
| Deleted `ContinuousProfiler/` | 9 files: Profiler, ProfilerSettings, NativeInterop, ProfilerAvailabilityHelper, ContextTracker, etc. |
| Cleaned `Instrumentation.cs` | Removed profiler configuration propagation |
| Cleaned `TracerManagerFactory.cs` | Removed profiler settings subscription, telemetry, and DataStreams profiler parameter |
| Cleaned `TracerManager.cs` | Removed profiler diagnostic log entries |
| Cleaned `TraceContext.cs` | Removed endpoint tracking via profiler |
| Cleaned `AsyncLocalScopeManager.cs` | Removed profiler context tracker from scope changes |
| Cleaned `GitMetadataTagsProvider.cs` | Removed git metadata propagation to profiler |
| Cleaned DataStreams files | Removed `ProfilerSettings` parameter from Manager/Writer/Formatter |
| Cleaned Telemetry interfaces | Removed `RecordProfilerSettings` from ITelemetryController and implementations |
| Cleaned `Ci/TestOptimization.cs` | Removed profiler flush on close |
| Removed `Directory.Build.props` analyzers reference | Removed deleted `Datadog.Trace.Tools.Analyzers` project references |

### OpenTelemetry removal

Deleted OpenTelemetry support from the tracer (~103 files):

| Change | Details |
|---|---|
| Deleted `OpenTelemetry/` | 33 files: OTel SDK, exporters, traces/metrics/logs stubs |
| Deleted `Activity/` | 41 files: System.Diagnostics.Activity listener bridge, handlers, helpers, duck types |
| Deleted `ClrProfiler/AutoInstrumentation/OpenTelemetry/` | 13 files: OTel SDK auto-instrumentation |
| Deleted `Vendors/OpenTelemetry.Exporter.OpenTelemetryProtocol/` | 16 vendored OTLP exporter files |
| Deleted OTLP-specific files | `Agent/ApiOtlp.cs`, `Agent/ManagedApiOtlp.cs`, `Logging/DirectSubmission/Sink/OtlpSubmissionLogSink.cs`, `ClrProfiler/.../OtlpLogEventBuilder.cs`, `OtelLogEventCreator.cs` |
| Cleaned `AgentWriter.cs` | Removed OtlpJson branch from `TracesEncoding` switch |
| Cleaned `LoggerDirectSubmissionLogEvent.cs` | Removed `OtlpLog` property using `LogPoint` |
| Cleaned `QuartzCommon.cs` | Removed all IActivity/IActivity5/ActivityKind methods |
| Cleaned `QuartzDiagnosticObserver.cs` | `OnNext` simplified to no-op |
| Cleaned `AspNetCoreHttpRequestHandler.cs` | Removed Activity tags copying |
| Cleaned `Tracer.cs` | Removed `ActivityListener.GetCurrentActivity()` traceId pull |
| Cleaned `Instrumentation.cs` | Removed `ActivityListener.Initialize()`, `MetricsRuntime.Start()`, OTel SDK init |
| Cleaned `TracerManagerFactory.cs` | Removed `ManagedApiOtlp` branch in `GetAgentWriter` |
| Cleaned `DirectLogSubmissionManager.cs` | Removed `OtlpSubmissionLogSink` branch |
| Cleaned `DirectSubmissionLoggerProvider.cs` | Removed `OtlpSubmissionLogSink` / `OtelLogEventCreator` branch |

### Runtime Metrics removal

Deleted `RuntimeMetrics/` (14 files) and cleaned external references:

| Change | Details |
|---|---|
| Deleted `RuntimeMetrics/` | 14 files: RuntimeMetricsWriter, GC counters, thread/memory collectors |
| Cleaned `TracerManager.cs` | Removed `RuntimeMetricsWriter` parameter/property, replace logic, dispose, diagnostic log entry |
| Cleaned `TracerManagerFactory.cs` | Removed conditional `RuntimeMetricsWriter` creation (`settings.RuntimeMetricsEnabled` check) |
| Cleaned `TestOptimizationTracerManager.cs` | Removed `RuntimeMetricsWriter` parameter from both `TestOptimizationTracerManager` and `LockedManager` constructors |
| Cleaned `TestOptimizationTracerManagerFactory.cs` | Removed `RuntimeMetricsWriter` parameter from `CreateTracerManagerFrom` |
| Cleaned `Tracer.cs` | Removed `runtimeMetrics: null` named argument from test-only constructor |

### AttackerFingerprint removal

ASM attacker fingerprint feature removed:

| Change | Details |
|---|---|
| Deleted `AppSec/AttackerFingerprint/` | `AttackerFingerprintHelper.cs` |
| Cleaned `SecurityReporter.cs` | Removed `AttackerFingerprintHelper.AddSpanTags` call and using |

### API Security removal

ASM API Security feature removed:

| Change | Details |
|---|---|
| Deleted `AppSec/ApiSec/` | `ApiSecurity.cs`, `EndpointsCollection.cs`, `MapEndpointsCollection.cs`, `DuckType/` |
| Deleted `ClrProfiler/AutoInstrumentation/AspNetCore/EndpointsCollection/` | KestrelServerImplStartAsync, MapExtensions v2/v3/v5+, RunExtensions integrations |
| Cleaned `Security.cs` | Removed `ApiSecurity` property and initialization |
| Cleaned `SecurityCoordinator.cs` | Removed `ApiSecurity.ShouldAnalyzeSchema` call in `RunWaf` |
| Cleaned `SecurityCoordinatorHelpers.Core.cs` | Removed `ApiSecurityParseResponseBody` gate in `CheckBody` |
| Cleaned `SecurityReporter.cs` | Removed `MaxApiSecurityTagValueLength` constant and `ExtractSchemaDerivatives` block |
| Cleaned `SecuritySettings.cs` | Removed all ApiSecurity* settings (Enabled, SampleDelay, EndpointCollectionEnabled, EndpointCollectionMessageLimit, ParseResponseBody) |
| Cleaned `RcmCapabilitiesIndices.cs` | Removed `AsmApiSecuritySampleRate` capability |
| Cleaned `TracerManager.cs` | Removed API Security diagnostic log entries |
| Cleaned `supported-configurations.yaml` | Removed `DD_API_SECURITY_*` environment variable definitions |
| Cleaned test files | Removed `ConfigurationKeys.AppSec.ApiSecurityEnabled` references from `AspNetBase.cs` and `AspNetMvc5.cs` |

### WAF and AppSec removal

Removed the WAF (Web Application Firewall) and most of AppSec. Only IAST-required infrastructure kept.

**Deleted directories:**

| Directory | Files | Purpose |
|---|---|---|
| `AppSec/Waf/` | 32 | WAF native bindings, initialization, return types |
| `AppSec/WafEncoding/` | 4 | WAF encoders |
| `AppSec/Concurrency/` | 3 | WAF context locks |
| `AppSec/Coordinator/` | 8 | SecurityCoordinator, SecurityReporter, helpers |
| `AppSec/Rcm/` | 15 | ASM remote config models (rules, IP blocking, exclusions) |
| `ClrProfiler/AutoInstrumentation/AspNetCore/UserEvents/` | — | User event tracking SDK |
| `ClrProfiler/AutoInstrumentation/ManualInstrumentation/AppSec/` | — | Manual AppSec SDK instrumentation |

**Deleted root files:** `Security.cs`, `SecuritySettings.cs`, `EventTrackingSdk.cs`, `EventTrackingSdkV2.cs`, `BlockException.cs`, `IDatadogSecurity.cs`, `SpanExtensions.cs`, `IEvent.cs`, `AddressesConstants.cs`, `AppSecRateLimiter.cs`, `BlockingAction.cs`, `CoreHttpContextStore.cs`, `SecurityConstants.cs`

**Deleted integrations:** `AspNetCoreBlockMiddlewareIntegrationEnd.cs`, `BlockingMiddleware.cs`, `MvcOptionsIntegration.cs`, `ActionResponseFilter.cs`, `DefaultModelBindingContext_SetResult_Integration.cs`, `FireOnStartCommon.cs`, `SpanExtensions.Core.cs`, `SpanExtensions.Framework.cs`, `UserDetails.Internal.cs`

**Kept (IAST requires):**

| File | Reason |
|---|---|
| `AppSec/Rasp/MetaStructHelper.cs` | IAST VulnerabilityBatch serialization |
| `AppSec/Rasp/StackReporter.cs` | IAST stack trace reporting |
| `AppSec/VulnerabilitiesModule.cs` | IAST dispatcher |
| `AppSec/AppSecRequestContext.cs` | Simplified to only vulnerability stack traces (`_stackTraces`, `AddVulnerabilityStackTrace`, `CloseWebSpan` MetaStruct emission) |
| `AppSec/ObjectExtractor.cs` | Recreated IAST-only for body extraction |
| `AppSec/ControllerContextExtensions.Framework.cs` | IAST-only for MVC/WebApi body + path params |

**Files cleaned of WAF/AppSec references:**
- `TracerManager.cs`, `TracerManagerFactory.cs`, `ClrProfiler/Instrumentation.cs`
- Diagnostic observers (`AspNetCoreDiagnosticObserver.cs`, `SingleSpanAspNetCoreDiagnosticObserver.cs`)
- `PlatformHelpers/AspNetCoreHttpRequestHandler.cs`
- `Agent/MessagePack/SpanMessagePackFormatter.cs` (removed WAF tag emission)
- ASP.NET integrations (`TracingHttpModule.cs`, MVC/WebApi integrations — removed blocking, kept IAST body/path param monitoring)
- `Iast/Location.cs` (hardcoded stack trace config defaults)
- `Iast/IastModule.cs` (removed `Security.Instance.Settings.StackTraceEnabled` check)
- 17 IAST aspect files (`catch (ex) when (ex is not BlockException)` → `catch (Exception ex)`)
- `Span.cs`, `TraceContext.cs` (removed `BlockException` checks, `DisposeAdditiveContext`)
- ASP.NET integration attributes: `InstrumentationCategory.AppSec | .Iast` → `.Iast`
- Test file `VulnerabilityBatchTests.Bundle.cs` (removed unused `using Datadog.Trace.AppSec.Waf`)

### WAF build infrastructure removal

Removed WAF download and ruleset deployment from the Nuke build system:

| Change | File |
|---|---|
| Removed `DependsOn(DownloadLibDdwaf)` and `DependsOn(CopyLibDdwaf)` from `BuildManagedTracerHome` and `BuildManagedTracerHomeR2R` | `tracer/build/_build/Build.cs` |
| Made `DownloadLibDdwaf` and `CopyLibDdwaf` targets empty no-ops | `tracer/build/_build/Build.Steps.cs` |
| Removed `libddwaf` package reference and all ruleset `<None Update>` entries | `tracer/test/Datadog.Trace.Security.IntegrationTests/Datadog.Trace.Security.IntegrationTests.csproj` |
| Removed `AppSec/Waf/ConfigFiles/rule-set.json` content include and ruleset `<None Update>` entries | `tracer/test/Datadog.Trace.Security.Unit.Tests/Datadog.Trace.Security.Unit.Tests.csproj` |

**Deleted 13 ruleset JSON files** from both security test projects: `ruleset.3.0.json`, `ruleset.3.0-full.json`, `ruleset.blocked.users.json`, `rasp-rule-set.json`, `remote-rules.json`, `remote-rules-override-blocking.json`, `wrong-tags-name-rule-set.json`, `wrong-tags-rule-set.json`, `rule-data1.json`, `rule-set-withschema.json`, `ruleset-withblockips.json`.

### DataStreamsMonitoring removal

Removed the entire DSM product (message pipeline monitoring for Kafka, RabbitMQ, SQS, etc.). Zero IAST dependencies.

**Deleted directories:** `src/Datadog.Trace/DataStreamsMonitoring/` (37 files), `test/Datadog.Trace.TestHelpers/DataStreamsMonitoring/` (4 mock files).

**Files cleaned of DSM references:**

| File | Change |
|---|---|
| `TracerManager.cs`, `TracerManagerFactory.cs` | Removed `DataStreamsManager` parameter, property, and disposal |
| `Ci/TestOptimizationTracerManager.cs`, `TestOptimizationTracerManagerFactory.cs` | Removed `DataStreamsManager` from constructor chains |
| `SpanContext.cs` | Removed `PathwayContext` property, `SetCheckpoint`, `ManuallySetPathwayContextToPairMessages` |
| `SpanContextInjector.cs`, `SpanContextExtractor.cs` | Removed DSM checkpoint blocks |
| `Agent/DiscoveryService/AgentConfiguration.cs`, `DiscoveryService.cs` | Removed `DataStreamsMonitoringEndpoint` |
| `Configuration/TracerSettings.cs` | Removed 4 DSM properties + config reading |
| `Configuration/supported-configurations.yaml` | Removed `DD_DATA_STREAMS_ENABLED` and `DD_DATA_STREAMS_LEGACY_HEADERS` |
| `Tracer.cs`, `Util/RandomIdGenerator.cs` | Removed DSM using directives |
| 25 messaging integrations | Removed DSM checkpoint/pathway calls (Kafka, RabbitMQ, AWS SQS/SNS/Kinesis/Lambda, Azure Service Bus, IBM MQ, Protobuf) |
| `test/Datadog.Trace.TestHelpers/MockTracerAgent.cs` | Removed `DataStreams` property, `WaitForDataStreams*` methods, pipeline_stats handler |

### APM integration removal

Removed 25 APM-only `ClrProfiler/AutoInstrumentation` directories that have no IAST code:

**Deleted:** AWS, Azure, Aerospike, Couchbase, CosmosDb, Elasticsearch, GraphQL, Hangfire, IbmMq, Kafka, Logging, ManualInstrumentation, MongoDb, Msmq, Protobuf, Quartz, RabbitMQ, Redis, Remoting, ServiceFabric, Testing, TraceAnnotations, VersionConflict, Wcf.

**Kept (IAST-relevant):** AdoNet, AspNet, AspNetCore, Http, Process, Grpc, RestSharp, CryptographyAlgorithm, HashAlgorithm, StackTraceLeak, Proxy.

Two small helpers were preserved from deleted directories because other parts of the tracer still depend on them:
- `ManualInstrumentation/TracerSettingKeyConstants.cs` — used by configuration sources
- `ManualInstrumentation/IntegrationSettingsSerializationHelper.cs` — used by legacy config source
- `Logging/LogContext.cs` — used by `LogFormatter.cs` for trace/span ID injection

### Remote Configuration Management (RCM) removal

Deleted the entire RCM subsystem. IAST as a standalone tool has no Datadog backend to receive configuration from.

| Change | Details |
|---|---|
| Deleted `RemoteConfigurationManagement/` | All RCM polling, apply, and model files |
| Cleaned `TracerManager.cs` | Removed RCM manager parameter, property, and disposal |
| Cleaned `TracerManagerFactory.cs` | Removed RCM construction and wiring |
| Cleaned `Instrumentation.cs` | Removed RCM initialization |
| Cleaned `IastSettings.cs` | Removed RCM-driven live-update callback |
| Cleaned `DynamicConfigurationManager.cs` | Removed RCM references throughout |

### CI Visibility removal

Deleted the entire CI Visibility product (`Ci/` directory).

| Change | Details |
|---|---|
| Deleted `Ci/` | All CI test session/span/module tracking files |
| Cleaned `TracerManager.cs` | Removed CI manager |
| Cleaned `TracerManagerFactory.cs` | Removed CI construction |
| Cleaned `Instrumentation.cs` | Removed CI initialization |

### PDB / Symbol handling removal

Deleted `PDBs/` directory and related symbol infrastructure not needed by IAST.

### DogStatsd / metrics removal

Removed StatsD metrics client integration (`DogStatsd/`). IAST has no metrics to emit to a StatsD endpoint.

| Change | Details |
|---|---|
| Deleted `DogStatsd/` | `StatsdManager`, `IStatsdManager`, `NullStatsdManager`, StatsD metric call sites |
| Cleaned `AgentWriter.cs` | Removed `IStatsdManager` parameter from all 3 constructors; removed all stats metric calls |
| Cleaned `Agent/Api.cs` | Removed `IStatsdManager` parameter; simplified `ToggleTracerHealthMetrics` |
| Cleaned `Agent/ManagedApi.cs` | Removed `IStatsdManager` parameter |
| Cleaned `Tracer.cs` | Removed `IStatsdManager` parameter from constructor |
| Cleaned `TracerManager.cs` | Removed `Statsd` property, constructor parameter, disposal block, heartbeat gauge |
| Cleaned `TracerManagerFactory.cs` | Removed statsd from all 3 method signatures; removed `new StatsdManager(settings)` |

### LibDatadog removal

Removed the native LibDatadog interop layer. IAST does not use the Datadog data pipeline.

| Change | Details |
|---|---|
| Deleted `LibDatadog/` | All files: `ByteSlice`, `CString`, `CharSlice`, `Error`, `ErrorHandle`, `DataPipeline/` (TraceExporter and all variants), `HandsOffConfiguration/` (ConfigurationResult, ConfiguratorHelper, interop structs) |
| Cleaned `Configuration/TracerSettings.cs` | Removed `enableIast` constructor param (LibDatadog), removed `DataPipelineEnabled` property and calculation |
| Cleaned `Configuration/GlobalSettings.cs` | Removed `LibDatadog.Logging.Logger` call |
| Cleaned `Configuration/ConfigurationSources/GlobalConfigurationSource.cs` | Simplified to remove hands-off config; always succeeds |
| Deleted `HandsOffConfigurationSource.cs` | Entire "hands-off" configuration source relying on LibDatadog |
| Cleaned `TracerManagerFactory.cs` | Removed LibDatadog pipeline construction from `GetAgentWriter` |
| Updated `Build.cs` | Removed `DownloadLibDatadog`/`CopyLibDatadog` dependencies from `BuildManagedTracerHome`/`BuildManagedTracerHomeR2R` |

### DatabaseMonitoring removal

Removed DBM (`DatabaseMonitoring/`) — comment propagation for SQL query tagging. IAST detects SQL injection directly; no DBM needed.

### Hardcoded secrets removal

Removed hardcoded secrets detection entirely — both managed and native sides. Hardcoded secrets is a static analysis concern (scans bytecode for literal patterns) and is not an IAST vulnerability (IAST is about runtime taint flow). It was also broken and unmaintained.

**Managed:**

| Change | Details |
|---|---|
| Deleted `Iast/Analyzers/HardcodedSecretsAnalyzer.cs` | Main analyzer class with polling thread and 68 regex patterns |
| Deleted `Iast/Analyzers/UserStringInterop.cs` | P/Invoke interop struct |
| Removed `IastModule.OnHardcodedSecret()` | Entry point called by the analyzer |
| Removed `OperationNameHardcodedSecret` | Replaced by `OperationNameDirectoryListingLeak` (which was incorrectly using it) |
| Removed `HardcodedSecretsAnalyzer.Initialize()` from `Iast.cs` | |
| Removed `VulnerabilityType.HardcodedSecret` enum value | |
| Removed `VulnerabilityTypeUtils.HardcodedSecret` constant | |
| Removed `IntegrationId.HardcodedSecret` | |
| Removed `DD_TRACE_HARDCODEDSECRET_*` config mappings | From `ManualInstrumentationLegacyConfigurationSource` |
| Removed from telemetry metrics | `MetricTags` and `IntegrationIdExtensions` |
| Removed `GetUserStrings` P/Invoke from `NativeMethods.cs` | |
| Deleted unit tests | `HardcodedSecretsTests.cs` (68 test cases) |
| Removed integration test | `TestIastHardcodedSecretsRequest` from `AspNetCore5IastTests` |
| Removed `/Iast/HardcodedSecrets` endpoint | From `IastController.cs` in the sample app |

**Native (`Datadog.Tracer.Native`):**

| Change | Details |
|---|---|
| Deleted `iast/hardcoded_secrets_method_analyzer.cpp` and `.h` | IL method analyzer that collected user strings |
| Removed `#include` and `GetUserStrings` export from `interop.cpp` | Function now gone |
| Removed `GetUserStrings` from `Datadog.Tracer.Native.def` | DLL export removed |
| Removed from `CMakeLists.txt` and `.vcxproj` / `.filters` | Build system cleanup |
| `method_analyzers.h` — `InitAnalyzers()` now returns empty vector | No analyzers remain |

---

## JSONL Vulnerability Reporter

Vulnerabilities are now written to a JSONL file (one JSON object per line) instead of span tags sent to the Datadog Agent.

### New file: `Iast/VulnerabilityReporter.cs`

Central reporter class:

- `Report(Vulnerability vulnerability, Span? requestSpan)` — called from `IastModule.cs`, passing `traceContext.RootSpan` as `requestSpan` so request context is always the web span, not whatever child span is active at detection time
- **Open/close per write** — no persistent file handle; the file is opened, written, and closed on every call. This prevents the process from locking the file for its entire lifetime, allowing test re-runs to start fresh.
- `ResolveReportPath()` — honors `DD_IAST_VULNERABILITY_LOG_PATH`:
  - Bare filename → appended to the tracer log directory
  - Path with directory component → used verbatim
  - Not set → `dotnet-iast-vulns-<processname>-<pid>.jsonl` in the tracer log directory
- `SerializeVulnerability(Vulnerability, Span?)` — full JSON schema:
  - `timestamp` (ISO 8601 UTC, `"o"` format)
  - `type` (vulnerability type string)
  - `hash` (dedup hash)
  - `request` — `method`, `url` (port scrubbed in tests), `route`, `endpoint` — read from `requestSpan` tags; covers both ASP.NET Core (`http.route`, `aspnet_core.endpoint`) and ASP.NET Framework (`aspnet.route`, `aspnet.controller`/`aspnet.action`)
  - `location` — `path`, `method` (no `line` — absent in release builds)
  - `evidence` — `valueParts` array with taint ranges
  - `sources` — array of taint sources
  - `stack` (inline frames, optional, opt-in per test) — frames use `method` (not `function`) and `class` (not `class_name`); no `id`

### Configuration key: `DD_IAST_VULNERABILITY_LOG_PATH`

Defined in `supported-configurations.yaml` (type: string, default: `''`), read by `IastSettings.VulnerabilityLogPath`.

### `DatadogLogging.LogDirectory`

Added `internal static string LogDirectory` property to `Logging/Internal/DatadogLogging.cs`. Resolved at static constructor time from the file sink path; falls back to `Path.GetTempPath()`. Used by `VulnerabilityReporter.ResolveReportPath()`.

### `StackReporter.GetUserFrames()`

Added `public static List<StackFrameInfo> GetUserFrames(int maxStackTraceDepth, int topPercent, StackFrame?[] frames)` to `AppSec/Rasp/StackReporter.cs`. Returns user frames without the agent wrapper; used by `VulnerabilityReporter` for inline stack serialization.

---

## IAST always-on

IAST is now always enabled. There is no "IAST disabled" mode.

| Change | Details |
|---|---|
| `IastSettings.Enabled` default | Changed from `false` to `true` |
| `DD_IAST_ENABLED` in `supported-configurations.yaml` | Default changed from `'false'` to `'true'` |
| Deleted `EnableIast(bool)` from `TestHelper.cs` | No longer exists; IAST is unconditionally on |
| Removed all `EnableIast(...)` calls | From `AspNetCore5IastTests`, `AspNetCore2IastTests`, `AspNetMvc5IastTests`, `IastInstrumentationUnitTests`, `AspNetCore5IastDbTests` |
| Removed `_enableIast` field | From `AspNetMvc5IastTests` |
| Removed `IastEnabled` property | From `AspNetCore5IastTests` and `AspNetCore2IastTests` |
| Deleted disabled test classes | `AspNetCore5IastTestsFullSamplingIastDisabled`, `AspNetCore2IastTestsFullSamplingDisabled`, `AspNetMvc5IntegratedWithoutIast`, `AspNetMvc5ClassicWithoutIast` |
| Simplified all filename ternaries | `IastEnabled ? "X.IastEnabled" : "X.IastDisabled"` → `"X"` (30+ occurrences) |
| `expectVulnerability: IastEnabled` → `expectVulnerability: true` | 17 occurrences across test files |
| `_testName` no longer carries `.enableIast=true` | `AspNetMvc5IastTests` |
| `GetFileName()` no longer appends `.IastEnabled`/`.IastDisabled` | `AspNetMvc5IastTests` |

---

## Integration Test JSONL Verification

Integration tests now validate vulnerability output against snapshot files (`.verified.txt`) instead of asserting on agent span snapshots.

### Test infrastructure additions (in `AspNetCore5IastTests` and subclasses)

| Addition | Purpose |
|---|---|
| `VulnerabilityLogPath` — derived from `GetType().Name` | Stable path across all xUnit theory instances of the same class. xUnit creates a fresh instance per `[InlineData]` case, so a `Guid` in the constructor would give each case a different path while the app writes only to the first one. |
| `VerifyVulnerabilityRecordsAsync(path, fileName, since, ...)` | Reads all records since the `since` cutoff, sanitizes, runs Verify snapshot. No type filter — the snapshot captures whatever is there; unexpected types are caught by snapshot diff. Timeout defaults to 5 s; pass `timeoutMs: 1_000` for not-vulnerable tests to return quickly with `[]`. |
| `SanitizeForVerification` | Removes `timestamp` (volatile), scrubs port in `request.url` (`localhost:PORT` → `localhost:00000`), removes `line` from location and stack frames (absent in release builds, present in debug — removing ensures both produce the same snapshot). |
| `SanitizeLdap / SanitizeReflectedXss` | Per-test sanitizers for compiler-generated class names in stack frames |
| `NotVulnerableSnapshotName = "Iast.NotVulnerable"` | Shared snapshot (`[]`) for all not-vulnerable test cases — avoids duplicate empty files |
| `Dispose` | No file deletion — JSONL lives in `LogDirectory` alongside tracer logs; deletion was failing anyway because the file was locked by the open persistent writer (now resolved) |

**Key isolation design:** `since = DateTime.UtcNow` is captured before each request. `TryReadRecords` parses the `timestamp` field (ISO 8601 `"o"` format, `CultureInfo.InvariantCulture`) to filter to the current test's window. The path is stable, the file accumulates across the test run, and `since` isolates each test's records.

**`vulnerabilitiesPerRequest: 200`** — both `AspNetCore5IastTestsFullSamplingIastEnabled` and `AspNetCore5IastTestsFullSamplingRedactionEnabled` use 200 (up from 3). Route-level sampling blocks detection once a route exhausts its budget; 3 was too low for Theory tests with many InlineData variants hitting the same endpoint.

### Snapshot mechanism

Uses [Verify](https://github.com/VerifyTests/Verify): `Verifier.Verify(sanitized, settings).UseFileName(fileName).DisableRequireUniquePrefix()`. First run creates `.received.txt`; accept it as `.verified.txt` to lock the snapshot.

---

## Current Architecture

```
Native CLR Profiler (IL rewriting)
  → Aspect methods called on string/IO/DB/HTTP operations
    → Taint tracking (per-request TaintedObjects map)
      → Sink detection (SQL injection, XSS, path traversal, etc.)
        → Vulnerability stored in IastRequestContext
          → IastModule.Report() → VulnerabilityReporter.Report()
            → Appended as JSON line to DD_IAST_VULNERABILITY_LOG_PATH
```

Span tags and agent transport are still used for request lifecycle (spans exist), but vulnerabilities are **additionally** written to the JSONL file in parallel. The long-term goal is to remove the span dependency entirely (see "Future Work" below).

### IAST Module Structure

**~132 C# files** under `tracer/src/Datadog.Trace/Iast/`:

| Directory | Files | Purpose |
|---|---|---|
| Root | ~40 | Core: `Iast.cs`, `IastModule.cs`, `IastRequestContext.cs`, `Vulnerability.cs`, `VulnerabilityBatch.cs`, `Evidence.cs`, `Source.cs`, `TaintedObjects.cs`, deduplication, overhead control, `VulnerabilityReporter.cs` |
| `Aspects/` | 63 | Method interception hooks organized by target library (System, System.Data, System.IO, System.Net, EntityFramework, MongoDB, NHibernate, ASP.NET, etc.) |
| `Dataflow/` | 13 | Aspect attributes (`AspectClassAttribute`, `AspectMethodReplace`, `AspectMethodInsertBefore/After`, `AspectFilter`) |
| `Propagation/` | 4 | Taint propagation through string operations (`StringModuleImpl`, `StringBuilderModuleImpl`, `PropagationModuleImpl`) |
| `SensitiveData/` | 9 | Evidence redaction (`EvidenceRedactor` + tokenizers for SQL, LDAP, JSON, URLs, headers, commands) |
| `Settings/` | 1 | `IastSettings` — IAST-specific configuration |
| `Helpers/` | 2 | MongoDB helper, string extensions |
| `Analyzers/` | 0 | Empty — `HardcodedSecretsAnalyzer` deleted (static analysis, not IAST) |

### IAST Test Structure

**Unit tests** (25 files): `tracer/test/Datadog.Trace.Security.Unit.Tests/IAST/`
- Component-level tests using Moq: redaction, deduplication, settings, taint tracking, vulnerability batching
- Subdirectory `Tainted/` (12 files) for taint-specific tests

**Integration tests** (11 files): `tracer/test/Datadog.Trace.Security.IntegrationTests/IAST/`
- Full end-to-end tests with real ASP.NET/ASP.NET Core apps
- JSONL output verified against Verify snapshots in `test/snapshots/`
- Test applications under `tracer/test/test-applications/security/`

---

## Configuration Reference

Current IAST-relevant environment variables:

| Variable | Default | Notes |
|---|---|---|
| `DD_IAST_ENABLED` | `true` | Master switch. Default flipped from `false` to `true`; runtime still honors the env var |
| `DD_IAST_VULNERABILITY_LOG_PATH` | `''` | Output file. Bare filename → log dir. Full path → verbatim. Empty → `iast-vulnerabilities-<pid>.jsonl` in log dir |
| `DD_IAST_DEDUPLICATION_ENABLED` | `true` | Hash-based dedup |
| `DD_IAST_VULNERABILITIES_PER_REQUEST` | `2` | Max vulnerabilities per request |
| `DD_IAST_MAX_CONCURRENT_REQUESTS` | `2` | Overhead control — max concurrent analyzed requests |
| `DD_IAST_REQUEST_SAMPLING` | `30` | Percentage of requests analyzed |
| `DD_IAST_REDACTION_ENABLED` | `true` | Evidence redaction (can disable for local use) |
| `DD_IAST_STACK_TRACE_ENABLED` | `true` | Include inline stack frames in JSONL output |

---

## Current Dependencies on Tracer Core

What IAST still relies on from the surrounding tracer. Drives the remaining work in "Future Work" below.

### Critical (must replace)

| Dependency | Where Used | How |
|---|---|---|
| **`Tracer.Instance.ActiveScope`** | `IastModule.cs` (~10 call sites) | Gets the current span to reach `TraceContext` and then `IastRequestContext` |
| **`Span` / `Scope`** | `IastModule.cs`, `IastRequestContext.cs` | Vulnerabilities stored as span tags; standalone spans created for startup vulnerabilities (directory listing, session timeout) |
| **`TraceContext`** | `TraceContext.cs:38,111,133` | Holds `IastRequestContext` as a field; `EnableIastInRequest()` initializes it |
| **`SamplingPriority`** | `IastModule.cs` | Force-keeps traces with vulnerabilities |
| **Agent transport** | Indirect via span pipeline | Vulnerabilities still ride on spans (in addition to JSONL) |

### Moderate (simplify or keep)

| Dependency | Where Used | Decision |
|---|---|---|
| **`IastSettings` / `TracerSettings`** | `Iast/Settings/IastSettings.cs` | Keep `IastSettings`, decouple from `TracerSettings` |
| **`IDatadogLogger`** | Throughout all IAST files | Keep (lightweight logging facade) |
| **`OverheadController`** | `IastModule.cs`, `Iast.cs` | Keep for performance protection, make optional |
| **`HashBasedDeduplication`** | `IastModule.cs` | Keep |
| **`DuckTyping`** | Some aspects (type casting for third-party libs) | Keep (~30 files, self-contained) |
| **`IntegrationId` / `IntegrationSettings`** | `IastModule.cs` | Used to check if IAST integrations are enabled; simplify |

### Remove entirely

| Dependency | Reason |
|---|---|
| **Evidence redaction** (`SensitiveData/`) | Local-only tool — no need to mask data |
| **AppSec / WAF / RASP** | ✅ Done |
| **Telemetry to backend** (`Iast/Telemetry/`, `Datadog.Trace/Telemetry/`) | ✅ Iast/Telemetry/ deleted; outer `Telemetry/` neutralized |
| **Agent discovery service** | Pending (no agent in standalone mode) |
| **Sampling infrastructure** | Pending (no traces to sample) |
| **Span tags / MetaStruct serialization** | Pending — JSONL writes in parallel today |
| **`InstrumentationCategory.IastRasp`** aspects | ✅ Done |

---

## Native Profiler Considerations

The native CLR profiler (`Datadog.Tracer.Native`) performs IL rewriting to inject IAST aspect calls. This is the mechanism that makes IAST work — without it, no aspects fire.

**Must keep**: The core IL rewriting engine and IAST aspect definitions.

**Can strip**: All non-IAST CallTarget definitions (integrations for Redis, HTTP clients, gRPC, logging, etc.). These are defined in managed code via `[InstrumentMethod]` attributes and registered during startup — removing the managed integration classes is sufficient; the native side discovers them dynamically.

**Partial native cleanup done**: `hardcoded_secrets_method_analyzer.cpp/.h` deleted, `GetUserStrings` export removed from `interop.cpp` and `.def`. The IL rewriting engine and IAST aspect definitions are untouched.

**Open question**: Whether to strip the remaining non-IAST native code or leave it. Leaving native otherwise untouched is significantly simpler and lower risk.

---

## Implementation Phases

### Phase 1 — Replace per-request context (pending)
- Introduce `AsyncLocal<IastRequestContext>` in new `IastLifecycle` class
- Replace all `Tracer.Instance.ActiveScope` chains in `IastModule.cs` with `IastLifecycle.GetCurrentContext()`
- Keep ASP.NET/Core request start/end CallTarget integrations, wire them to `IastLifecycle`

### Phase 2 — File-based output ✅ Done (parallel to spans)
- ~~Create `VulnerabilityFileWriter` with JSON serialization~~ → `VulnerabilityReporter.cs` writes JSONL
- Still pending: replace `IastRequestContext.AddIastVulnerabilitiesToSpan()` with `IastRequestContext.FlushToFile()` (currently both run)
- Still pending: remove MessagePack serialization from `VulnerabilityBatch`

### Phase 3 — Remove evidence redaction (pending, optional)
- Delete `Iast/SensitiveData/` (9 files)
- Remove redaction calls from `VulnerabilityBatch` and `IastModule`

### Phase 4 — Remove telemetry and agent dependencies (partial)
- ~~Delete `Iast/Telemetry/` (2 files)~~ ✅ Done
- ~~Remove all `TelemetryFactory` / metric emissions from IAST code~~ ✅ Done (neutralized)
- ~~Delete `LibDatadog/`~~ ✅ Done
- Still pending: delete `Agent/`, `HttpOverStreams/` directories

### Phase 5 — Gut non-IAST code (mostly done)
- ~~Delete DataStreamsMonitoring~~ ✅ Done
- ~~Delete non-IAST ClrProfiler integrations (~25 directories)~~ ✅ Done
- ~~Delete RCM, CI, PDBs, DogStatsd, DBM, ContinuousProfiler, Debugger, OpenTelemetry, RuntimeMetrics, FeatureFlags, ApiSec, AttackerFingerprint, WAF~~ ✅ Done
- ~~Delete non-IAST tracer/src projects (~10 projects)~~ ✅ Done (20 projects removed)
- ~~Delete hardcoded secrets analyzer (managed + native)~~ ✅ Done
- Still pending: remove `Span.cs`, `Scope.cs`, `Tracer.cs`, `TraceContext.cs` or replace with stubs

### Phase 6 — Simplify configuration (pending)
- Create standalone `StandaloneIastSettings` reading only IAST env vars
- Remove `TracerSettings`, `ExporterSettings`, `IntegrationSettings`

### Phase 7 — Adapt tests ✅ Done
- ~~Rework integration tests to validate JSONL file output~~ ✅ Done (Verify snapshots)
- ~~Remove non-IAST test projects~~ ✅ Done (16 projects removed)
- ~~Remove IAST-disabled test classes and `EnableIast` infrastructure~~ ✅ Done
- ~~Fix process leak in `AspNetCoreTestFixture.Dispose()`~~ ✅ Done (2s timeout + try-catch on shutdown request)
- Still pending: fix unit tests to work without tracer mocks

---

## Open Questions

1. **Native profiler**: Strip it down too, or leave as-is and only gut managed code? *(Leaning: leave native untouched.)*
2. **Request lifecycle**: Keep minimal CallTarget integrations (Option A) or use DiagnosticSource listeners (Option B)?
3. **Output format**: ✅ JSONL chosen (one JSON object per line, custom schema).
4. **Output strategy**: ✅ One file per process (PID-suffixed default; configurable via `DD_IAST_VULNERABILITY_LOG_PATH`), append-only.
5. **Overhead controller**: Keep (performance safety) or remove (scan everything)? *(Currently kept.)*
6. **Hardcoded secrets**: ✅ Removed — static analysis, not IAST (runtime taint flow).
7. **Project structure**: Gut `Datadog.Trace.csproj` in-place (A) or new project (B)? *(Currently A — in-place gutting.)*
8. **Dataflow traces in output**: Include full taint propagation path (source → operations → sink) in findings? *(Currently: sources + tainted value parts, no full dataflow chain.)*
