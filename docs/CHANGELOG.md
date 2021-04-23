# Datadog .NET Tracer (`dd-trace-dotnet`) Release Notes

## [Release 1.26.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.26.1)

## Changes
* Serialize tags/metrics in a single pass to improve performance (#1416)
* Add Ducktype reverse proxy for implementing interfaces indirectly (#1402)

## Fixes
* Don't throw or log exceptions in TryDuckCast methods (#1422)
* Fix git parser on really big pack files (>2GB) in CIApp (#1413)

## Build / Test
* Reinstate the consolidated multi-stage build pipeline (#1363)
* Enable endpoint routing in aspnetcore benchmark (#1418)
* Re-enable AspNet integration tests in CI (#1414)
* Update NuGet packages in integration tests, under existing instrumentation version ranges (#1412)

[Changes since 1.26.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.26.0...v1.26.1)

## [Release 1.26.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.26.0)

## Changes
* Compute top-level spans on the tracer side (#1302, #1303)
* Add support for flushing partial traces (#1313, #1347)
  * See [the documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/#experimental-features) for instructions on enabling this feature.
* Enable Service Fabric Service Remoting instrumentation out-of-the-box (#1234)
* Add log rotation for native logger (#1296, #1329)
* Disable log rate-limiting by default (#1307)
* CallTarget refactoring and performance improvements (#1292, #1305, #1279)
* CIApp: Add a commit check before filling the commiter, author and message data (#1312)
* Update ASP.NET / MVC / WebApi2 Resource Names (#1288)
  * See [the documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-framework/#experimental-features) for instructions on enabling this feature.
* Update ASP.NET Core Resource Names (#1289)
  * See [the documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/#experimental-features) for instructions on enabling this feature.
* Report tracer drop-rate to the Trace Agent (#1306, #1350, #1406)
* Update URI "cleaning" algorithm to glob more identifier-like segments and improve performance (#1327)
* Upgrade Serilog & Serilog.Sinks.File Vendors (#1345)
* Update OpenTracing dependency from 0.12.0 to 0.12.1 (#1385)
* Include PDB symbols in MSI installer, and linux packages (#1364, #1365)
* Generate NuGet package symbols (#1401)
* Improve `DD_TRACE_HEADER_TAGS` to decorate web server spans based on response headers (#1301)

## Fixes
* Fix Container Tagging in Fargate 1.4 (#1286)
* Increase buffer size to avoid edge cases of span dropping (#1297)
* Don't set the service name in the span constructor (#1294)
* Replace `Thread.Sleep` with `Task.Delay` in dogstatsd (#1326, #1344)
* Fix double-parsing not using invariant culture (#1349)
* Fix small sync over async occurrence in DatadogHttpClient (#1348)
* Delete accidentally pushed log file (#1408)

## Build / Test
* Add additional ASP.NET Core tests + fix response code bug (#1269)
* Minor build improvements (#1295, #1352, #1359, #1403)
* Crank importer and pipeline (#1287)
* Add benchmarks for calltarget (#1300)
* Define benchmarks scheduled runs in yaml (#1299, #1359)
* Call a local endpoint in DuplicateTypeProxy test (#1308) 
* Fix components in `LICENSE-3rdparty.csv` file (#1319)
* Enable JetBrains Tools in the Benchmarks projects (#1318)
* Started work on a consolidated build pipeline (#1320, #1335)
* Add Dependabot for keeping dependencies up to date (#1338, #1361, #1387, #1399, #1404)
* Improvements to flaky tests (#1271, #1331, #1360, #1400)
* Add further test resiliency against assembly loading issues (#1337)
* Additional testing for TagsList behaviour (#1311)
* Fix native build to allow specifying configuration (#1309, #1355, #1356, #1362)
* Add benchmark for Serilog log injection (#1351)
* Fix Datadog.Trace.Tests.DogStatsDTests.Send_metrics_when_enabled (#1358)
* Don't run Unit test or runner pipelines on all branch pushes (#1354)
* Add additional test for ContainerID parsing (#1405)
* Fixes the CMake version 3.19.8 in CMakeLists (#1407)

[Changes since 1.25.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.25.0...v1.26.0)

## [Release 1.25.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.25.0)

## Changes
 * Runtime metrics are publicly available. They can be enabled by setting the `DD_RUNTIME_METRICS_ENABLED` environment variable to `1`. For more information: https://docs.datadoghq.com/tracing/runtime_metrics/dotnet/
 * Changes in the trace buffering logic (#1151) :
   * Traces are now serialized as soon as possible, instead of every second. This reduces the lifetime of Span objects, which in turn should decrease the number of gen 1/2 garbage collections
   * Whenever adding a trace would cause the buffer to overflow, the contents are immediately flushed. This should reduce the number of dropped traces for customers with a very large amount of spans
 * Optimizations in the native profiler (#1224, #1217, #1215)
 * Duck-typing: rename typing cast methods to better reflect the intent (#1220), and add a `DuckIgnore` attribute (#1257)
 * Disable log rate limit when debug logging is enabled (#1239)
 * CallTarget instrumentation:
    * Add support for Redis (#1230)
    * Add support for GraphQL (#1241)
    * Add support for MongoDB (#1214)
    * Add support for ASP.NET MVC and WebAPI (#1208)
    * Add support for CurlHandler (#1252)
    * Add support for Elasticsearch (#1248)
    * Add support for RabbitMQ (#1186)
    * Add support for WCF (#1272)
    * Refactor HttpMessageHandler-based instrumentations (#1258) and enable them by default (#1277)
    * Add fast-path for integrations with 7 or 8 parameters (#1261)
    * Enable inlining by default (#1276)
    * Change log severity (#1278)

 * Various changes to CI integration (#1242, #1247, #1251, #1244)

## Fixes
 * Fix some log messages (#1240)
 * Status was incorrectly reported for NUnit tests with no assertions (#1235)
 * Strengthen type check in the method resolution (#1225) and ducktyping (#1291). This should fix some `BadImageFormatException` errors when loading assemblies into different load contexts 
 * Remove sync-over-async when communicating to the agent through named pipes in AAS (#1218)
 * Calltarget:
    * Don't call `FindMemberRef` when the signature is empty (#1259)
    * Remove useless instruction in the emitted IL (#1267)
    * Properly return a faulted task when an exception is thrown in an instrumented async method (#1270)

## Build / Test
 * Update Moq to version 4.16.0 and Xunit to version 2.4.1 (#1227, #1231)
 * Update .NET SDK version to 5.0.103 (#1237)
 * Update log4net to 2.0.12 (#1243)
 * Fix Xunit serialization in tests (#1236)
 * Update the automatic logs injection sample apps (#1195)
 * Add a Service Fabric sample app (#1190)
 * Improve ASP.NET integration tests (#1246)
 * Fix solution load deadlock for Rider on non-Windows OS (#1256)
 * Fix build errors in CallTargetNativeTest (#1254)
 * Update 3rd party license file (#1260)
 * Enable WCF integration tests (#1273)
 * Fix flaky tests (#1262, #1263, #1264, #1265, #1266)

Changes since 1.24.0: [All commits](https://github.com/DataDog/dd-trace-dotnet/compare/v1.24.0...v1.25.0) | [Full diff](https://github.com/DataDog/dd-trace-dotnet/compare/v1.24.0..v1.25.0)

---

### Release notes for releases before 1.25.0 can be found in the [releases page](https://github.com/DataDog/dd-trace-dotnet/releases) on GitHub.
