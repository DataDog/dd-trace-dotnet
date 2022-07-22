# Datadog .NET Tracer (`dd-trace-dotnet`) Release Notes







## [Release 2.12.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.12.0)

## Summary

- [Tracer/ASM] Adds horizontal propagation, and enables optional user id propagation
- [Profiler] Build for Linux Arm64
- [Tracer] Add support for latest StackExchange.Redis release

## Changes

### Tracer
* Propagate trace tags to downstream services (horizontal propagation) (#2897)
* Clean the way we build the tracer native code on Linux (#2943)
* Log the number of dropped spans when flushing the buffer (#2948)
* Ensure we release cached StringBuilder instances back to the StringBuilderCache (#2956)
* Allow user id to be propagated (#2968)
* Add support for StackExchange.Redis 2.6.48 (#2959)

### Continuous Profiler
* [Profiler] Run profiler tests on .NET 6 (#2909)
* [Profiler] Reenable sampling test (#2913)
* [Profiler] Fix parsing thread information (#2929)
* [Profiler] Disable walltime profiler during exception tests (#2935)
* [Profiler] Do not dereference nullptr to avoid crashing (#2940)
* [Profiler] Clean the way we build the profiler on Linux (#2942)
* [Profiler] Remove profiler managed code (#2946)
* [Profiler] Build and package the Profiler for Arm64 architecture (#2952)
* [Profiler] Bump libddprof version to 0.6.0 (#2958)

### Debugger
* Add instrumented IL verification (#2829)

### Build / Test
* Setup Code Scanning for dd-trace-dotnet (#2286)
* Add named pipe support to the mock agent + convert telemetry tests to Snapshot (#2816)
* Add smoke tests for the distribution NuGet on Linux (#2878)
* [Pipeline Monitoring] Make it run at the end in any case (#2890)
* Save loader and profiler native symbols from GitLab (#2896)
* [Test Package Versions Bump] Updating package versions (#2901)
* [codeowners] Rename ci-app-tracers to ci-app-libraries (#2919)
* Convert StackExchange.Redis to snapshots and add support for latest (#2921)
* Fix changed error message in Kafka 1.9.0 (#2922)
* Update usages of Newtonsoft.Json to 13.0.1 across test applications (#2923)
* [Tracer] Convert SQS integration tests to snapshots (#2926)
* [Tracer] Convert ServiceStackRedis integration tests to snapshots (#2927)
* Fix NuGet installer smoke tests not running (#2928)
* Allow queuing multiple connection for acceptance in UDS test agent (#2930)
* [Build] Fix S3 upload (#2932)
* [Tracer] Add throughput tests for stats computation (#2936)
* Add smoke tests for the MSI installer (#2937)
* Add smoke tests for windows-tracer-home.zip (#2941)
* Add smoke tests for dotnet-tool instrumentation on Windows (#2944)
* Add Smoke testing of the NuGet package on Windows (#2947)
* Add smoke tests for dotnet-tool instrumentation on Linux (#2949)
* Add support for alpine to the dd-trace tool (#2951)

### Miscellaneous
* Bump NuGet.CommandLine from 5.8.1 to 5.9.2 in /tracer/build/_build (#2907)
* Exclude guid-like assembly names from telemetry (#2954)


[Changes since 2.11.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.11.0...v2.12.0)

## [Release 2.11.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.11.0)

## Summary

- [Tracer Sampling] The tracing library no longer applies a default sampling rate of 100%. Instead, the library applies the sampling rates calculated in the Datadog Agent.
- [Linux Packaging] Make the `createLogPath.sh` executable again (issue introduced in 2.10.0)

## Changes

### Tracer
* Add support for computation of stats in the tracer (#2591)
* [Tracer] Take into account the default sampling values sent by the agent (#2836)

### CI App
* [CIApp] MSBuild logger improvements (#2894)

### Continuous Profiler
* [Profiler] Add CPU scenario to profiler throughput tests (#2864)
* [Profiler] Add iterator scenario in computer01 demo (#2889)
* [Profiler] Do not print stack walker error messages everytime (#2902)
* [Profiler] Don't sample if no CPU time was consumed nor in Ready state (#2903)

### Miscellaneous
* [Test Package Versions Bump] Updating package versions (#2877)

### Build / Test
* Replace use of semantic info with syntax-only check instead (#2892)
* Rename `isScheduledBuild` to `isBenchmarksOnlyBuild` (#2893)
* [Linux packaging] Make sure the createLogPath script is executable (#2911)


[Changes since 2.10.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.10.0...v2.11.0)

## [Release 2.10.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.10.0)

## Summary

Main changes are:
- Continuous profiler now available on linux (needs GLIBC 2.18+, which means Centos 7 is not supported in this release)
- CPU profiling and Exception profiling are available.

Also here are more minor changes on the Tracer integrations:
- `NLog` integration supports v5.0.0
- Stops prefixing `aspnet.request` resource name with IIS virtual apps in the rare cases it happened
- Fixes the incorrect `http.status_code` tag for ASP.NET MVC with IIS in Classsic-mode when an exception is thrown
- `MySqlConnector` support starts at v0.61.0

## Changes

### Tracer
* Add support for UDS to telemetry transport (#2617)
* Return const char* instead of std::string on C linkage function (#2824)
* Fix SamplingPriority in B3 and W3C headers (#2828)
* Add Redis obfuscator (#2847)
* Exclude dynamic assemblies from telemetry (#2855)
* Avoid calling AsyncLocal.Value twice when GetSpanContextRaw() is null (#2857)
* Rewrite P/Invoke maps for Native Loader (#2858)
* [Tracer] Support MySqlConnector from version 0.61.0 (#2870)
* Update NLog integration to be compatible with v5.0.0 (#2820)

### CI App
* [CIApp] - Per-test Code Coverage (#2739)
* [CIApp] - Adds library_version to the message metadata (#2831)
* [CIApp] - Refactor CodeOwner feature + Zero duration on Skipped Tests (#2845)

### ASM
* [ASM] Change to rate limit tests (#2759)

### Continuous Profiler
* [Profiler] Implement exception profiler (#2743)
* [Profiler] Update CPU profiling implementation (#2765)
* Package the profiler, tracer and native loader together on Linux (#2777)
* Add throughput tests for profiler (#2784)
* [Profiler] Fix error reported by UBSAN (#2799)
* [Profiler] Do not aggregate sample with empty callstack (#2805)
* [Profiler] Flush samples and export a last .pprof on exit (#2814)
* [Profiler] Add a cache for native frame resolution (#2817)
* [Profiler] Add a profile_seq tag to each uploaded .pprof (#2819)
* [Profiler] Allow stack sampler metrics for Linux (#2822)
* [Profiler] Implement sampling for exceptions (#2823)
* [Profiler] Activate log message only in debug build (#2825)
* [Profiler] Run profiler integration tests in AzDo (#2830)
* [Profiler] Fix empty labels (#2832)
* [Profiler] Fix profiler throughput test on master (#2837)
* [Profiler] Add async scenario to demo application (#2840)
* [Profiler] Disable debug logging for throughput tests (#2841)
* [Profiler] Fix analyzer error (#2844)
* [Profiler] Add exceptions throughput test scenario (#2856)
* [Profiler] Set _GLIBCXX_USE_CXX11_ABI to 0 and remove liblzma dependency (#2861)
* [Profiler] Run profiler benchmark tests with monitoring home instead for windows (#2862)
* [Profiler] Add CPU and exceptions to profiler benchmarks (#2865)
* [Profiler] Prevent from blocking the application (#2866)
* [Profiler] Do not resolve native frame if not needed (#2868)

### Serverless
* [Serverless] Support sampling in universal instrumentation (#2751)

### Fixes
* [CallTarget] - Fix Nested Types Instrumentation (#2818)
* Stop prefixing resource names with IIS app name when TracingHttpModule only (#2852)
* Fix incorrect `http.status_code` in ASP.NET MVC with IIS classic mode (#2854)

### Build / Test
* Execution Benchmark and Throughput cleanup (#2790)
* Replace wiremock with `dd-apm-test-agent` in smoke tests (#2793)
* [Test Package Versions Bump] Updating package versions (#2803)
* Speed up native compilation on Linux (profiler, tracer, native loader) (#2804)
* Use azure scale-sets for more jobs (#2806)
* Split linux integration tests to reduce overall runtime (#2807)
* Fix Gitlab job (#2821)
* Fix `ThreadAbortAnalyzer` to work with the latest .NET SDK (#2833)
* Split ARM64 integration tests to reduce overall run time (#2834)
* [Release] Changes following last release (#2835)
* Fix downstream job trigger for deployments to rel-env (#2838)
* Change code ownership (#2839)
* Use Native Loader for exploration tests (#2842)
* [Tests] Allow updating snapshots from an AzDo build (#2843)
* Add workaround for race condition in GRPC library (#2848)
* Increase timeout in `AspNetCoreDiagnosticObserver` tests (#2849)
* Revert "Use azure scale-sets for more jobs (#2806)" (#2850)
* Add snapshot tests for ASP.NET virtual applications (#2851)
* Fix flaky `LambdaRequestBuilderTests` (#2863)
* Fix crash in execution benchmark tests (#2873)
* Stripping out unneeded information from native `so` library files (#2874)
* Upload linux-native-symbols artifacts and add to release artifacts (#2879)
* Create a Code Freeze bot (#2881)
* Fix code freeze workflow (#2882)
* Fix code-freeze bot (part deux) (#2885)

### Miscellaneous
* [Test Package Versions Bump] Updating package versions (#2827)
* [Doc] Add a quick comment on docker for integrations (#2869)


[Changes since 2.9.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.9.0...v2.10.0)

## [Release 2.9.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.9.0)

## Summary

This version mainly contains Tracer bug fixes:
- Avoid a Stackoverflow exception in the context of ci-app or Azure functions when using different versions of the Tracer for automatic and custom instrumentation
- Fix the order of precedence of the parameters to connect to the Trace agent.


## Changes

### Tracer
* [Tracer] Fix precedence in ExporterSettings (#2737)
* Call get_Instance instead of GetDistributedTracer in case of version mismatch (#2767)
* Don't check agent connectivity when tracing is disabled (#2772)
* Fix number of spans in telemetry tests (#2786)
* Throw `ArgumentNullException` when trying to set `Tracer.Instance` to `null` (#2437)
* Instrument custom methods using New Relic attributes (#2780)

### CI App
* [CiApp] - Avoid setting env:none if not environment variable has been set (#2783)

### ASM
* [ASM] Little waf metrics changes (#2745)

### Continuous Profiler
* [Profiler] Remove classes/files part of the old pipeline (#2750)
* [Profiler] build profiler linux in AzDo (#2761)
* [Profiler] Rename `pid` tag into `process id` (#2789)
* [Profiler] Rename tests apps with "Samples." prefix instead of "Datadog.Demos." (#2788)

### Serverless
* Convert AWS lambda tests to using snapshots (#2681)

### Fixes
* Delete obsolete AutomapperTest sample (#2771)

### Build / Test
* [Release] Trigger AAS release automatically (#2721)
* Stop building 1.x branch daily (#2732)
* Monitor the pipeline from a final stage (#2746)
* [Build] Don't build twice docker image in ITests (#2760)
* Split benchmarks between 6 different agents (#2763)
* Fix all sample package versions and `UpdateVersion` task (#2766)
* Remove timeouts from .NET install and add default timeout (#2768)
* Write integration test logs directly to `build_data` directory (#2773)
* Refactor Windows Integration tests (#2779)
* Use the same .NET SDK version in GitHub Actions as we do in Azure Devops (#2781)
* Improve git checkout to handle rebases on Benchmark VMs (#2782)
* Run `git clean -ffdx` in checkout code (#2785)
* Add missing GraphQL 5 tests (#2757)

### Miscellaneous
* [All] - Refactor ITags/TagsList tracer implementation (#2753)
* Minor UDS support updates (#2787)
* Remove dependency to libuuid (#2792)
* [Test Package Versions Bump] Updating package versions (#2795)


[Changes since 2.8.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.8.0...v2.9.0)

## [Release 2.8.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.8.0)

## Summary

This version mainly:
* Solves a use case where the tracer would hang on startup, on CentOs.
* Runtime Metrics now matches traces for service names that were normalized by the Tracer.
* Changes the default resource name when using `[Trace]` attributes
* The profiler now reports the same service name, environment and version as the Tracer (even when configuring the Tracer by code)

## Changes

### Tracer
* Activity Compatibility Layer (#2446)
* Use native `HttpClient` for UDS in .NET 6 (#2665)
* Add more Debug logs in the tracer loader. (#2673)
* Refactor base endpoint calculations (#2688)
* Read profiler application info from tracer (#2699)
* [dnlib] Enable THREAD_SAFE preprocessor constant (#2718)
* Update default resource name for TraceAnnotationsIntegration (#2720)
* Improve profiler logging in the tracer (#2733)
* [DuckTyping] Two pass check to avoid creation of invalid types in memory. (#2716)
* Return `CORPROF_E_PROFILER_CANCEL_ACTIVATION` if Tracing is Disabled (#2685)
* Normalize DogStatsD client service name with Trace Agent logic (#2719)
* Add support for GraphQL 5.x (#2632)
* Fix hang on Centos and Improve installer smoke tests (#2726)

### AppSec
* [AppSec] Unhandled exception bug (#2698)
* [AppSec] Monitor path params for asp.net core 3.0 routing endpoints, webforms and webapi (#2715)
* [Appsec] Remove unused data model files (#2722)

### Continuous Profiler
* [Profiler] CPU profiling implementation (#2645)
* [Profiler] Add Undefined-Behavior sanitizer on the profiler (#2690)
* [Profiler] Fix duration computation in CPU time profiler (#2738)
* [Profiler] Allow Stack collector to collect callstack of the current thread (#2740)
* [Profiler] Rework the linux stack frames collectors (#2747)

### Miscellaneous

* [ALL] Add overload to create std::string from WCHAR* (+ size) (#2744)

### Build / Test
* Allows our pipeline to be monitored (#2704)
* Convert GraphQL tests to snapshot tests (#2707)
* Try building samples in parallel (#2709)
* [Tests] Remove Datadog.Trace references from samples (#2710)
* Add additional entry to version bump files (#2714)
* Add tests for extension methods with DD_TRACE_METHODS (#2717)
* If we kill the process in our GraphQL tests on Linux, raise a `SkipException` (#2727)
* [Pipeline Monitoring] Works better with an agent (#2728)
* Consolidate custom frameworks (#2729)
* Delete Samples.MultiDomainHost test application and its dependencies (#2731)
* Use ARM64 AWS auto scale group (#2734)
* Don't wait forever for a hanging sample to end (#2735)
* Rework RabbitMQ sample to try avoid race condition (#2736)
* Refactor Windows Build and clean Beta Msi related code (#2742)
* Use a separate agent pool for running status updates (#2749)
* [Build] Add timeout to dotnet installs (#2752)
* Try using local git configuration instead of global (#2755)

[Changes since 2.7.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.7.0...v2.8.0)


## [Release 2.7.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.7.0)

## Summary

This version includes:
-  Support for automatic custom tracing using `[Trace]` attributes, using [Datadog.Trace.Annotations](https://www.nuget.org/packages/Datadog.Trace.Annotations)
- API for easier correlation with distributed tracing
- [Profiler] The Code Hotspot feature is now available: You can now see profiles associated to your traces
- [AppSec] New API for setting the user associated with a trace


## Changes

### Tracer
* Add net6.0 target (#2405, #2658)
* [Tracer] Add API to extract SpanContext (#2694)
* If DirectLogSubmission is enabled, enable Logs Injection by default (#2697)
* Adds Tracer Side SQL Query Obfuscator (#2498)
* Automatically instrument methods decorated with `[Trace]` (`Datadog.Trace.Annotations.TraceAttribute`) (#2606)
* Add wildcard support to DD_TRACE_METHODS (#2628)
* Update Aerospike integration to support 5.0.0 (#2639)

### AppSec
* [AppSec] Api to allow user data to be associated with the local root span (#2546, #2682, #2706)
* [AppSec] Update waf and ruleset 1.3.0 (#2638)
* [Appsec] Call waf only once, add path params address (#2676)
* Activate and expose the WAF's obfuscator (#2696)
* [AppSec] Move AppSec config keys to their own file (#2700)

### Continuous Profiler
* [Profiler] Use shared runtime id in the profiler (#2635)
* [Profiler] Stop building the profiler outside of the repository folder (#2651)
* [Profiler] Use libddprof-based pipeline by default + cache value (#2659)
* [Profiler] Enable CodeHotspot feature by default when the profiler is enabled (#2660)
* [Profiler] Add beta revision in the profiler version (#2664)
* [Profiler] Fix Code Hotspots tests (#2669)
* [Profiler] Do not compute a default output path for pprof files (#2671)
* [Profiler] Make local testing easier (#2680)
* [Profiler] Add new job: Address sanitizer on Linux (#2687)
* [Profiler] Do not export empty profiles (#2701)
* [Profiler] Bump profiler beta version (#2702)
* [Profiler] Add the profiler deployment script to the bumping process (#2705)

### Serverless
* [Serverless] Send the lambda invocation context to our extension (#2656)

### Build / Test
* CLI - Display the version of the profiler and tracer (#2594)
* Ensure we test a consistent commit for PRs (#2649)
* Update GraphQL tests to take a memory dump when not shutting down (#2655)
* Add simplistic labeller (#2662, #2674, #2683)
* Fix build in gitlab (#2666, #2678)
* Add retries to more flaky steps (#2679)
* Fix "MakeGrpcToolsExecutable" when NugetPackageDirectory is not provided (#2689)
* Make running integration tests on OSx great again (#2708)

### Miscellaneous
* Disable warnings in vendored code (#2672)
* Remove remaining references to nlohmann.json NuGet package (#2691)
* Move `loading dynamic library` mechanism to shared code (#2624)

[Changes since 2.6.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.6.0...v2.7.0)


## [Release 2.6.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.6.0)

## Summary

This version mainly brings:
* Tracing for Grpc.AspNetCore.Server, Grpc.Net.Client and Grpc.Core. More information in [the public doc](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core/#integrations)
*  Correlation between Live Process and APM (#2544)

## Tracer
* Remove Duplicate Content-Type  (#2492)
* Add support for Grpc.AspNetCore.Server and Grpc.Net.Client (#2535)
* add `process_id` tag to root span during serialization (#2544)
* Add support for Grpc.Core (#2545)
* Add timeouts to remote requests (#2572)
* Do not load the tracer when loading NInject temporary appdomain (#2600)
* Add additional diagnostic for Tags source generator (#2610)
* Fix `DD_TRACE_METHODS` issue with manual-automatic version mismatch (#2621)
* [Log Submission] Fallback to using GlobalTags for direct log submission (#2636)
* Fix tag concatenation for direct log submission (#2642)

## CI App
* [CIAPP] - CODEOWNERS support (#2596)
* [CIApp] - Update payload metadata format to latest spec (#2603)
* Add null checks and remove the possibility to do wrong type casting (#2609)
* [CIApp] - Implement specific CLI Run CI Settings and Descriptions (#2625)
* [CiApp] Fix null environment variable in payload (#2640)

## AppSec
* [Appsec] Scanning request body by the waf (#2495)
* Fix log message (#2623)
* Update WAF rules to 1.2.7 (#2641)

## Continuous Profiler
* [Profiler] Use smart_pointer to manage services lifetime (#2570)
* [Profiler] Implement Code Hotspot in the profiler (#2588)
* [Profiler] Fix profiler build (#2619)
* Rework the Native Logging API for shared code (#2620)

## Serverless
* [Serverless] Send lambda response payloads (#2608)
* [Serverless] Merge two serverless debug lines into one (#2637)

## Fixes
* NGEN - Refactor rejit handler to remove inliners on module unload. (#2602)
* [CLI] Correctly handle end of stream in ProcessMemoryStream  (#2616)

## Build / Test
* Add Tracer testing and more repos to exploration tests (#2308)
* Remove direct Datadog.Trace assembly references in integration tests (#2586)
* [Release] Remove Tracer MSI and Improve release notes categorization (#2607)
* Start allowing patch versions on master (#2613)
* Improve source generator verification (#2614)
* Allow setting `force_run_exploration_tests_isTracerChanged` variables (#2615)
* Upload snapshots directory as an artifact (#2618)
* [Profiler] Use Monitoring-home package in Profiler Rel.Env. (#2622)
* [Release] Add profiler files to version bump (#2630)
* [Release] Upload the windows-tracer-home file from GitLab (#2631)
* Reorder Trace Annotation tests to reduce flakiness (#2633)
* Fix issue where PdbReaderTests fail when running locally but pass in CI (#2646)


[Changes since 2.5.1](https://github.com/DataDog/dd-trace-dotnet/compare/v2.5.1...v2.6.0)

## [Release 2.5.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.5.1)

## Summary

* From now on, _Datadog Continuous Profiler_ is released within the MSI on Windows. It is deactivated by default. Set the environment variable `DD_PROFILING_ENABLED=1` to enable it.
* You can now instrument methods through configuration thanks to the `DD_TRACE_METHODS` variable. Public documentation change is in progress (Install 2.6.0 instead if you want to test this feature as we have fixed one issue when using different versions between Custom and Automatic instrumentation). 
* Finally, the release also brings support for B3 and W3C headers  (#2536).

## Tracer

### Changes
* Add clarification to automatic instrumentation docs (#2473)
* Add `ExpandRouteTemplatesEnabled` setting for ASP.NET Core (#2496)
* stop propagating `x-datadog-tags` header (#2507)
* Add ASP.NET support for `DD_TRACE_EXPAND_ROUTE_TEMPLATES_ENABLED` (#2517)
* If the `int` status code is not valid, don't set it (#2529)
* Managed loader cache (#2533)
* SpanContextPropagator refactor (#2536)
* Instrument custom methods through configuration (DD_TRACE_METHODS) (#2543)
* Add DD_TRACE_RATE_LIMIT parameter (#2557)
* Add small amount of Debug logs for runtime metrics submission (#2582)
* Refactor all sync over async disposals (#2583)
* Adds Timeout support to RequestFactories Http implementations. (#2548)

### Fixes
* handle `language` tag during serialization and add to manual spans (#2542)
* Add missing `volatile` in Tracer creation (#2508)
* Adds missing using statement and fix telemetry deadlock (#2574)
* Fix error in HttpWebRequest timeout value. (#2581)

### CLI Tool
* Make ProcessEnvironmentWindows more robust (#2499)
* Add checks for the profiler path in the CLI (#2482)
* Update the CLI tests to use the new EnvironmentHelper methods (#2528)
* CLI checks: consolidate IIS output messages when listing sites/apps (#2532)
* Fix CLI tool failing process checks when using Native Loader (#2551)
* Increase the retry delay in ProcessEnvironmentWindows (#2577)

## CI App
* [CI App] - Agentless Writer (#2402)
* [CIApp] - Add support for XUnit LogsDirectSubmission (#2531)
* [CI-App] Adds test.bundle tag (#2561)
* [CIApp] - Protect foreground thread with a timeout to avoid blocking process shutdown (#2564)
* [CIApp] - Fixes some agentless tags (#2575)
* [CIApp] - Small improvements. (#2584)
* [CIApp] - Add agentless url configuration key (#2589)

## AppSec
* Remove some CI appsec tests since blocking is not supported yet (#2505)
* Increase WAF timeout in tests (#2512)
* [Appsec] Update rules file (#2516)
* [AppSec] Support postfix time format for WAF timeout (#2519)
* [AppSec] small fix: don't log a warning if no waf timeout has been specified (#2576)

## Continuous Profiler
* Reverse to protobuf 3.7.0 to avoid unexpected new dependency (System.Memory.dll) (#2513)
* [Profiler] Implement new pipeline to generate .pprof via libddprof (#2524)
* [Profiler] Backport feature: send heartbeat metric on each export (#2565)
* [Profiler] Prevent additional/useless copies in logging wrappers (#2585)
* [Tracer] Implement Code Hotspot feature (#2587)
* Fix warnings (#2518)
* [Profiler] Use managed code to generate pprof file (#2540)

## Shared
* Allow sharing runtime ids between Tracer and Profiler (and other) (#2474)
* Ensure the native loader works with 3rd party dispatcher (e.g.: Contrast) (#2478)
* Add better handling of nullable type chaining (#2424)
* Shared Loader - Add support for more rewriting points (#2487)
* Add proper spdlog-based logging to NativeLoader (#2489)
* [Cleanup] Move shared classes/files to shared and ensure Profiler, Tracer and Native Loader use them (#2493)
* [Shared Loader] - Fix variable name (#2504)
* [All] Use custom `std::filesystem` implementation for Linux (#2527)
* Add basic PDB reading capability (and vendor-in dnlib) (#2592)
* Update joint MSI with original .NET Tracer configuration (#2597)
* Remove manual call to logger shutdown (#2559)

## Build / Test
* Make all integration tests utilize Native Loader instead of loading `Datadog.Trace.ClrProfiler.Native` directly (#2358)
* Improve error handling in CLI (#2463)
* Increase Redis test matrix (#2472)
* Ignore common test failures (#2477)
* Fixes for the build related to hotfix releases (#2511)
* Add artifacts for the standalone tool (#2514)
* Run profiler pipeline on PRs to hotfix branches (#2522)
* Allow disabling samples on Alpine, and always running only major versions (#2534)
* Reduce number of integration tests built and run on master (#2547)
* Add  `--overwrite true` to Azure upload (#2556)
* Use Azure VMSS hosted runners (#2560)
* Allow skipping builds for some packages on ARM64 (#2562)
* Update test packages and fix MongoDb sample (#2563)
* Fix Yaml in .gitlab-ci.yml (#2566)
* Use CustomTestFramework in runner integration tests (#2590)
* Strengthen GraphQL span expectation to address test flakiness (#2593)
* CLI - Check for suspicious registry keys in WOW6432Node (#2595)
* Fix typo in docker-compose.yml that is causing an error to appear in CI (#2475)
* [Profiler] Add profiler in the automatic version bump process (#2530)
* [Profiler] Fix build_windows_profiler AzOp step (#2541)
* [Tracer] Use the native loader for Windows integration tests (#2549)
* [Profiler] Activate checks in smoke tests for the new pipeline (#2552)
* Run system tests over UDS (#2571)
* Improve docs on Linux builds with Docker on Windows (#2523)

## Misc
* Use mermaid for flow chart (#2476)
* Remove TODO from stylecop.json (#2490)

[Changes since 2.4.4](https://github.com/DataDog/dd-trace-dotnet/compare/v2.4.4...v2.5.1)



## [Release 2.4.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.4.0)

## Changes
* Add Telemetry (#2241, #2431, #2454, #2410)
* Support cleaning of filenames in HttpClient + ASP.NET (#2471)
* Add warning when attempting to use UDS on < .NET Core 3.1 (#2412)
* Make IntegrationRegistry and DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS case insensitive (#2447)
* [CIApp] - Revert `ci_library.language` tag to `language` (#2433)
* [CIApp] - Ensure the Origin tag value on the public SpanContext .ctor (#2434)
* Update protobuf version to 3.19.4 (#2443)
* Refactor all EnumNgenModuleMethodsInliningThisMethod call sites (#2470, #2442)
* Refactorings to avoid merge conflicts with the Live Debugger (#2452)
* Add support for trace-level tags (vertical propagation) (#2432)
* Updates to CLI command (process checks, IIS checks, version conflict detection) (#2416, #2450, #2448)

## Fixes
* Fix MongoDB integration displaying incorrect query (#2435, #2430)
* Update the http.status_code on root-level aspnet.request spans after TransferRequest calls (#2419)
* clean up `StartSpan()` and `StartActive()` overloads in `Tracer` (#2406)
* Remove unused `IApiRequest.PostAsJson()` method (#2411)
* Fix missing `http.status_code` tag on ASP.NET Core spans with errors (#2458)
* Always set response header tags in ASP.NET Core (#2480)

## Build / Test
* Improvements in `SpanContextPropagatorTests` (#2414)
* Improvements to CI reliability (#2438, #2449, #2451, #2453, #2456, #2457, #2462, #2467, #2468)
* Add regression tests for running in partial trust environments (#2338)
* Include ad-hoc integration versions for coverage (#2311)
* Update README badges (#2418)
* Trigger deployment to the reliability environment (#2362)
* Update `.vsconfig` (#2407)
* Setup Github Actions for the profiler CI (#2415)
* Add SampleHelpers.CreateScope (#2427)
* Update PR template (#2429)
* Add Auth header to HttpClient that downloads Azure DevOps build artifacts (#2436)
* New tags useful for throughput testing (#2439)
* Increase coverage in mongodb integration tests (#2469)
* Describe the best scenarios to upgrade from v1 to v2 (#2417)
* Adding more documentation on instrumenting specific methods (#2441)

[Changes since 2.3.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.3.0...v2.4.0)


## [Release 2.3.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.3.0)

## Changes
* Refactor Rejit Handling to support debugger use case (#2250)
* Add internal "start" api to `Tracer`/`TracerManager` (#2330)
* Implement agent connectivity checks + add UDS support (#2337)
* Check the registry in the CLI (#2403)
* [AppSec] Upgrade waf to 1.0.17, Support Arm and fix unimportant memory leak (#2357)
* [AppSec] Configurable timeout for the WAF (#2400)
* [CIApp] - Adds CI Library Language and Version according to CIApp specs. (#2388)
* [CIApp] - Change CIEnvironmentValues from static to singleton. (#2389)
* [Serverless] interact with extension (#2352)
* [Serverless] add void return type support for AWS Lambda (#2413)

## Fixes
* Remove `TraceContext.SamplingPriority` setter (#2368)
* use `int` internally instead of `enum` for sampling priority values (#2372)
* Don't log to Console.WriteLine (#2379)
* remove unused `FormatterResolverWrapper` (#2384)
* [AppSec] Fix incorrect type arguments to AppSec Debug log (#2392)

## Build / Test
* Use SkippableFact in payload tests (#2343)
* Small fix for environment configuration source tests (#2361)
* Skip Cosmos tests for now (#2364)
* Re-use ultimate pipeline on forked repositories (#2373)
* Add support for ServiceStack.Redis 6.x (#2374)
* Explicitly allow public reads on objects we upload to S3 from gitlab (#2376)
* Update FluentAssertions to 6.4.0 (#2386)
* Fix the benchmark project build (#2391)
* Bind the agent in the CLI tests (#2393)
* Display a warning when a test has been running for too long (#2396)
* [Gitlab] More explanations on the --grant option used to upload artifacts (#2397)
* [Serverless] Fix AWS Lambda integration tests (#2408)
* [Release] A few enhancements after last release (#2387)
* Fairly minimal .vsconfig (#2401)
* Move profiler source files into the tracer repository (#2404)
* Add profiler x64 MSI to releases (#2409)


[Changes since 2.2.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.2.0...v2.3.0)


## [Release 2.2.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.2.0)

## Changes
* Add support for direct log submission (#2240, #2332)
* Add UDS Transport (#2142)
* [AppSec] Prepare the public beta of the product
  * Rate Limiter for appsec traces (#2350)
  * Conform to new standard logs docs and remove some spammy logs (#2129)
  * Refactoring of instrumentation gateway, call security at end request for 404 rules (#2251)
* [Serverless] auto instrument lambda handler (#2233)
* [AAS] Allow using custom metrics or tracing without automatic tracing (#2186)
* [AAS] Azure functions enabled by default, but still unpluggable (#2326)
* [AAS] Default debug logs to false (#2314)
* [CLI] Invest in our CLI tool to ease onboarding
  * Add process checks to the CLI  (#2323)
  * Add a test to detect when the CLI home folder is missing (#2320)
  * Add new command line arguments (#2295)
  * Migrate CLI to Spectre.Console  (#2254)
  * Add artifact tests for the dd-trace tool artifacts (#2280)
* Add SpanContext.None (#2309)
* Add initial support for x-datadog-tags propagation header (horizontal propagation) (#2178)
* Move some static readonly fields to const (#2292)
* Documentation
  * Add documentation for new InstrumentationDefinitions generator (#2317, #2310)
  * Delete outdated blog entry (#2304)
* Add TryParse function from WSTRING to int (#2369)

## Fixes
* Fix recording for `405 Method Not Allowed` in ASP.NET Core integration (#2333)
* Add `#if NETFRAMEWORK` to System.Web integrations #2294
* Avoid potential exception in Security constructor #2235

## Build / Test
* [DEPENDABOT] Ignore Patches (#2315)
* [Build] Add a no-op pipeline for PRs without code changes (#2378, #2347, 2334, 2303, #2296, #2342, 2287)
* [Release] Make the rellease process faster (#2377, #2375, #2351, #2346, #2335, #2279)
  * Make releases instant(ish) (#2324)
  * Automatic upload of MSIs and Nugets (#2340)
* [Build] Add env variable in CI builds to select a specific ref for the continuous profiler build (#2345)
* Don't install the .NET tool in the CI build  (#2363)
* Add retry to cosmosdb initialization (#2348)
* Fix some test data (#2329)
* Bump the version of SourceLink  (#2319)
* Use a source generator to build InstrumentationDefinitions (#2288, #2305)
* Update Verify files and version (#2301)
* CI Improvements (#2306)
* Update TagsList generation output and add verification action (#2285)
* Run adhoc throughput test (#2248)
* [Test Package Versions Bump] Updating package versions (#2284)
* SpanContextPropagator unit tests (#2273)


[Changes since 2.1.1](https://github.com/DataDog/dd-trace-dotnet/compare/v2.1.1...v2.2.0)



## [Release 2.1.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.1.0)

## Changes
* Added support for instrumenting abstract methods (#2120, #2169)
  * Enables support for generic ADO.NET libraries, for example IBM.Data.Db2(#1494)
* Ignore ping commands in MongoDB (#2216)
* [CiApp] Include .NET 6.0 in dd-trace tool (#2206)
* Add additional constructor to TracerSettings for discoverability (#2266)
* Performance improvements and refactoring
  * Expand usage of StringBuilderCache (#2214)
  * Code clean up in `SpanContextPropagator` (#2180, #2261)
  * Add more attributes from `System.Diagnostics.CodeAnalysis` namespace (#2181)
  * Move logs-injection related classes to sub-folder (#2238)
  * Explicitly state contentType when calling IApiRequest.PostAsync() (#2237)
  * Remove unused `ITraceContext` (#2225)

## Fixes
* Support child actions in aspnet (#2139)
* Do not normalize periods in header tags if specified by the customer (#2205)
* Replace MongoDB/WCF reflection lookups with DuckTyping (#2183)
* Remove all LibLog code and simplify ScopeManager code (#2184)
* Fix shared installer version (#2277, #2209)

## Build / Test
* Add Exploration Tests on Linux (#2193)
* Remove unused test case for calltarget abstract (#2215)
* Consolidate mock span implementations (#2119)
* Add DuckType Best Practices documentation in Readme.md (#2223)
* Add Source Generator for TagsList (#2076, #2258, #2262)
* [Test Package Versions Bump] Updating package versions (#2145, #2219, #2231, #2246, #2276)
* Pipeline reliability updates (#2149, #2226)
* Fix flaky test in AspNetVersionConflictTests (#2189)
* Fix Datadog.Trace.sln build issues (#2192)
* Add more tests around public API (#2202)
* Rewrite logs injection benchmarks (#2204)
* Disable parallelization in runtime metrics tests (#2227)
* Remove ConcurrentDictionary in SpanStatisticalTests (#2229)
* Add a Nuke target to update snapshots (#2232)
* Fix benchmark allocation comparison (#2236, #2207)
* [AppSec] Improve testing (#2239)
* Add regression tests for the tool command line arguments (#2243, #2253, #2263)
* Add workaround to support VS2022 with Nuke (#2252)
* Run GraphQL tests against latest package versions (#2264)
* [Test Package Versions Bump] Updating package versions ()

[Changes since 1.31.1](https://github.com/DataDog/dd-trace-dotnet/compare/v1.31.1...v2.1.0)

## [Release 2.0.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.0.1)

## Changes
* Add README for Datadog.Trace NuGet package (#2069)
* Re-implement NLog logs injection (#2096)
* Remove LockSamplingPriority (#2150)
* Re-implement Serilog logs injection (#2152)
* Reset the AsyncLocal DistributedValue when starting an automatic instrumentation continuation (#2156)
* [Redo] Fix logs injection to work correctly when there's a version mismatch (#2161)
* Revert "Disable version conflict fix (#2155)" (#2162)
* Isolate transport settings (#2166)
* Fix parser null ref and upgrade rule-set (#2167)
* [AppSec] Handle null keys when reading headers, cookies and query strings (#2171)
* Synchronize runtime id across versions of the tracer (#2172)
* Fix build paths in README (#2175)
* Minor improvements (#2177)
* Include missing properties on Exporter Settings (#2179)
* [AppSec] Update rules file 1.2.4 (#2190)
* [2.0] Simplify Tracer.StartActive overloads to match ITracer.StartActive overloads (#2176)
* Changes types visibility (#2185)

## Fixes
* [2.0] - Skip loader injection on profiler managed loader (#2196)

## Build / Test
* [appsec] throughput tests (#2160)
* Rename profiler projects (#2187)
* Modify tests to use Tracer.StartActive(string, SpanCreationSettings) (#2191)
* Fix warnings in Nuke (#2174)
* * Properly disable parallelization in AzureAppServicesMetadataTests (#2173)

[Changes since 1.31.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.31.0...v2.0.1)

## [Release 2.0.0-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.0.0-prerelease)

## Changes
* Resolver and Formatter improvements for domain-neutral scenarios. (#1500)
* Remove unused throughput test (#1802)
* Couchbase instrumentation (#1925)
* [2.0] Add ImmutableTracerSettings (#1951)
* [2.0] Remove obsolete APIs (#1952)
* [2.0] Drop support for .NET < 4.6.1 (#1957)
* [2.0] - Remove CallSite instrumentation (#1958)
* [2.0] Update default value of DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED to true (#1961)
* [2.0] - Native stats fixes (#1967)
* [2.0] Make various APIs internal (#1968)
* [2.0] - Adds a simple agent port checker on CI Visibility scenarios. (#1972)
* [2.0] Mark deprecated settings obsolete (#1974)
* [2.0] Cleanup old factory method and tests (#1980)
* fold `IntegrationIds.ElasticsearchNet5` into `ElasticsearchNet` (#1983)
* [2.0] - Remove unnescessary copies from native side. (#1989)
* [2.0] Handle multiple tracer instances gracefully (#1990)
* Improves DomainMetadata class (#2003)
* [2.0] Split integration id `AdoNet` into individual ids per db type (#2008)
* [2.0] Use distributed tracing for compatibility across different versions of the tracer (#2020)
* [2.0] Rename ReplaceGlobalSettings() -> Configure() (#2023)
* [2.0] Various small DiagnosticObserver optimisations (#2024)
* [2.0] Use buster-based debian for building (#2032)
* Update CI App specs (#2035)
* Profiler uses environment variables instead of current module for pinvoke generation (#2042)
* Remove dependency from the shared project to main project (#2043)
* [2.0] - Detect current module at runtime and use it for PInvoke rewriting (#2044)
* [2.0] Relax duck type visibility requirements (#2045)
* [2.0] Simplify IntegrationSettingsCollection (#2048)
* [AppSec] Waf fixes: reading register for waf and unit tests for alpine (#2060)
* Restoring Couchbase instrumentation (#2064)
* Remove the newline between the log message and properties (#2070)
* Update supported ADO.NET versions (#2077)
* Remove CIApp Feature Tracking (#2088)
* Change the public API to only use ISpan / IScope (#2090)
* CallTarget ByRef (#2092)
* Add a pointer to the "Performance Counters and In-Process Side-By-Side Applications" doc (#2093)
* Add space in log line (#2094)
* Mock the active span when using multiple versions of the tracer (#2095)
* Add helper for collecting host metadata (#2097)
* Change the CallTargetReturn struct to a ref struct (#2103)
* [CIApp] - MsTest 2.2.8 integration. (#2107)
* Bump supported version of NUnit3TestAdapter (#2118)
* [AppSec] Fix tag prefix bug (#2123)
* Add a big fat warning in NativeCallTargetDefinition file (#2126)
* Use the non truncated UserAgent property in .netfx webapi (#2128)
* Update ITracer interface (#2131)
* Add space before exception message (#2133)
* Make Span internal (#2134)
* Improve performance of AutomaticTracer (#2135)
* Log additional information about the AssemblyLoadContext (#2136)
* Reinstate Integration.AdoNet integration (#2137)
* DBScopeFactory improvements. (#2138)
* Use PathBase.ToUriComponent in AspNetCoreDiagnosticObserver (#2141)
* Adds feature flag to disable integration version checks. (#2147)
* Disable version conflict fix (#2155)

## Fixes
* [2.0] Additional cleanup from CallSite removal (#1966)
* [2.0] - Refactor Integration struct, and remove unused code. (#1969)
* [2.0] - Remove unused files (#1985)
* [2.0] Cleanup some more obsolete usages (#1997)
* Remove SpanId argument from StartActiveWithTags (#2017)
* [2.0] Minor restructuring of logging files (#2040)
* [2.0] Add modified WCF CallTarget instrumentation via opt-in environment variable (#2041)
* Fix a couple of obsolete API usages (#2056)
* [2.0] remove leftover references to `integrations.json` and `DD_INTEGRATIONS` (#2073)
* [2.0] remove unused class `MethodBuilder` and test references (#2074)
* Fix disabled integrations log message (#2078)
* CallTarget ByRef: Fix ByRef Generics arguments (#2099)
* Fix host metadata kernel information (#2104)
* Fix benchmarks comparison (#2130)

## Build / Test
* [2.0] Add support for .NET 6 (#1885)
* [2.0] Update GitLab build to use .NET 6 SDK (#2010)
* [2.0] Fix ASP.NET Core tests + snapshots (#2012)
* [2.0] Add .NET 6 WebApplicationBuilder tests (#2016)
* Don't push artifacts to AWS/Azure unless on main/master (#2018)
* [2.0] Add Linux installer smoke tests (#2036)
* Switch to using curl to send AzDo build GitHub status (#2049)
* Try switch dependabot generation to use pull_request_target (#2055)
* Don't throw in AgentWriterBenchmark (#2057)
* Allow running ITests locally on Mac (#2063)
* [Test Package Versions Bump] Updating package versions (#2072)
* Implementation of Exploration Tests (#2089)
* Redesign package version generator to allow more granular control (#2091)
* Fix flaky test in unit tests (#2106)
* Centralize Mock Trace Agent Initialization (#2114)
* Update dependabot/Nuke targets  (#2117)
* Fix asp snapshots file names (#2148)

[Changes since 1.31.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.31.0...v2.0.0-prerelease)

## [Release 1.30.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.30.0)

## Changes
* Make vuln app database samples path work cross OS (#1824)
* Add the standardizes logs for AppSec (#1894)
* Refactor reverse duck-typing and add negative tests (#1900)
* Additional updates for the joint MSI (#1915)
* Correct _dd.appsec.enabled (#1918)
* Refactor ModuleMetadata handling to reduce memory allocations (#1931)
* [Appsec] Send events in utf 8 without byte marker (#1932)
* Use UserKeep(2)/UserReject(-1) in the rules-based sampler (#1937)
* Add AssemblyLoadContext to logs for netcoreapp assembly builds (#1977)
* Update tests and CI App environment variable parser fixes. (#1995)
* Cherry-pick/backport to master (#1996)
* [AppSec] Update waf version to 1.14  (#1998)
* Use sensible default service name in AAS (Functions & WebApps) (#2007)

## Fixes
* Shared Managed Loader delays loading assemblies until AD has initialized. (#1916)
* Fix typo in comments (#1928)
* Escape http.url tag in AspNetCore spans (#1938)
* Properly set sampling priority when using GetRequestStream (#1947)
* Fix issue with WAF with x86 on x64 (#1984)
* Add modified WCF CallTarget instrumentation via opt-in environment variable (#1992)
* Stop writing warning to log about ServiceFabric if we're not running in ServiceFabric (#1999)

## Build / Test
* Add test application for MySqlConnector nuget package (#1863)
* Build a beta MSI that contains multiple .NET products (#1870)
* Don't use ITestOutputHelper after test has finished (#1877)
* Improve testing for Aerospike (#1889)
* Upgrade to Alpine 3.14 (#1899)
* Remove the "comprehensive" testing suite (#1907)
* Clean the workspace before building on ARM64 (#1908)
* Create version.txt (#1917)
* Update Datadog.Trace to 1.19.1 in regression test (#1919)
* Don't downgrade cmake on OSX (#1920)
* Include the package version in all the MySqlConnector tests (#1921)
* Modify the GitLab CI to build and sign the multi-product MSI (#1924)
* Give some time for the aspnetcore process to exit in AppSec tests (#1926)
* Use GitHub API to hide old code coverage/benchmark reports when adding a new one (#1927)
* Run log injection tests on linux (#1929)
* Update dependabot PRs  (#1930)
* Small build fixes (#1936)
* Add a "LatestMajors" option for testing all major package versions (#1939)
* Add system tests into CI (#1942)
* Fix GitHub Actions workflows (#1946)
* Fix condition in Datadog.Trace.proj breaking the build on master (#1950)
* "Update test package versions" PR has to run UpdateIntegrationsJson (#1954)
* Treat release/ branches as the "main" branch (#1963)
* Use anyCPU on Mac as well for target: CompileRegressionDependencyLibs (#1994)
* Don't push artifacts to AWS/Azure unless on main/master (#2019)

[Changes since 1.29.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.29.0...v1.30.0)

## [Release 1.29.1-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.29.0)

## Changes
* [Appsec] Send events in utf 8 without byte marker (#1932)

[Changes since 1.29.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.29.0...v1.29.1-prerelease)

## [Release 1.29.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.29.0)

## Changes
* Re-implement log4net logs injection (#1710)
* [AppSec] Send ip addresses headers to the backend (#1764)
* CI Visibility Mode v1 (#1795)
* Add PInvoke Map rewriting in all platforms to support native library renaming (#1809)
* CallSite instrumentation - Add exceptions to active span during ASP.NET Web API 2 message handler exception (#1817)
* [CIVisibility] Add Test Framework version to test spans (#1828)
* Add lookup for `LocalRootSpanId` property and tests for `SpanId` and `LocalRootSpanId`.  (#1839)
* Add LifetimeManager for handling shutdown events (#1841)
* Report attack when response has ended (#1847)
* [CIVisibility] Update CI namespace types (#1864)
* Add tags for App Sec (#1869)
* Cleanup startup logs (#1890)
* Reduce the number of times we call the WAF (#1901)

## Fixes
* Skip WriteTrace call when no there's no spans and improve filtering (#1843)
* Fix bug logging response even if successful (#1874)
* [AppSec] Fix appsec event tag value (#1898)

## Build / Test
* Generate Dependabot File for Integrations (#1754)
* Scan integration test logs for errors (#1776)
* Post benchmark result comparison as a comment to the PR (#1811)
* Small build improvements (#1813)
* Dont test all integration package versions on every PR (#1818)
* Parallelise Windows integration tests by framework (#1819)
* Change more timeouts in MassTransit (#1829)
* Add a sleep to CallTargetNativeTests to minimize risks of segfaults on 2.1 (#1830)
* Fix path in gitlab.bat after repository move (#1832)
* Enable static analysis for ConfigureAwait (#1833)
* Update Gitlab build image to use 5.0.401 SDK (#1834)
* Skip segmentation faults in .NET Core 2.1 tests (#1835)
* Fix transient error in aspnetcore tests (#1836)
* Use temporary folder for NServiceBus storage (#1840)
* Add a separate performance pipeline for running Performance tests (#1842)
* Additional updates to launchSettings.json for test applications (#1848)
* Fix performance pipeline (#1851)
* Use Xunit.SkippableFact for inconclusive tests (#1858)
* Update test package versions (#1862)
* Add more information to aerospike tags (#1865)
* Kill the old Samples.Shared project (#1867)
* Delete performance pipeline, and manually update GitHub statuses (#1868)
* Initialize LocalDB ahead of time in the CI (#1873)
* Handle the expected segfault in smoke tests (#1878)
* Fix errors in the CI when the startup log thread gets aborted (#1879)
* Test the tracer works with F# web framework 'Giraffe' (#1888)
* Add sanity check for _OR_GREATER compiler directives (#1895)
* Skip Pipeline for Dependabot Lures (#1905)

[Changes since 1.28.8](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.8...v1.29.0)

## [Release 1.28.8](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.8)

## Changes
* Adds automatic instrumentation for GraphQL 3 and 4 (#1751)
* Adds automatic instrumentation for Elasticsearch 7 (#1760, #1821)
* Adds initial beta Azure Functions automatic instrumentation (#1613)
* Remove CallTarget Integrations from json file. Integrations are now loaded directly from the dll. (#1693, #1780, #1771)
* Add exceptions to active span during ASP.NET Web API 2 message handler exception (#1734 #1772)
* Refactor ILogger integration (#1740, #1798, #1770)
* Refactor repository folder locations (#1748, #1762, #1762, #1806, #1759, #1810)
* Updates to the shared native loader (#1755, #1825, #1826, #1815, #1729)
* AppSec updates (#1757, #1758, #1768, #1777, #1778, #1796)
* Improve DuckType generic methods support (#1733)
* Rename ADO.NET providers integration names (#1781)
* Enable shared logger for managed log file (#1788)
* Remove unused ISpan/IScope (#1746, #1749)

## Fixes
* Restore Tracer.ActiveScope in ASP.NET when request switches to a different thread (#1783)
* Fix duplicating integrations due to multiple Initialize calls from different AppDomains. (#1794)
* Fix reference to mscorlib causing failures with reflection (#1797)
* Propagate sampling priority to all spans during partial flush (#1803)
* JITInline callback refactor to fix race condition. (#1823)

## Build / Test
* Update .NET SDK to 5.0.401 (#1782)
* Improvements to build process and automations (#1812, #1704, #1773, #1792, #1793, #1799, #1801, #1808, #1814)
* Disable memory dumps in CI (#1822)
* Fix compilation directive for NET5.0 (#1731)
* Restore the original env-var value before asserting. (#1816)
* Catch object disposed exception in Samples.HttpMessageHandler (#1774)
* Add minimal test applications that use service bus libraries (#1690)
* Synchronously wait for tasks in HttpClient sample (#1703)
* Update test spans from Crank runs (#1592)
* Update code owners (#1750, #1785)
* Exclude liblog from code coverage by filepath (#1753)
* Move tracer snapshots to /tracer/test/snapshots directory (#1766)
* Increase timeout in MassTransit smoke tests (#1779)
* Fix CIEnvironmentVariable test (#1765)

[Changes since 1.28.7](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.7...v1.28.8)

## [Release 1.28.6](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.6)

## Changes
* Add support for Aerospike (#1717)
* Reduce native memory consumption (#1723)
* Implement 1.0.7 of libddwaf library for AppSec (#1732, #1742)
* Finalise naming of Datadog.Monitoring.Distribution NuGet package (#1720, #1728)

## Fixes
* AppSec Header keys should be lower case (#1743)

## Build / Test
* Updates to code coverage (#1699)
* Fixes for various flaky tests (#1713, #1715, #1718, #1719, #1722
* Package windows native symbols in a separate archive (#1727)
* Disable AppSec crank till a new runner machine can be created (#1739)
* Updates to shared loader build (#1724, #1725, #1735, #1736)

[Changes since 1.28.4](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.4...v1.28.6)

## [Release 1.28.5-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.5-prerelease)

## Changes
* Remove usage of non-builtin types in Attribute named arguments (#1601)
* Add Microsoft.Extensions.Logging.ILogger instrumentation (#1663)
* Adding appsec crank scenarios (#1684)
* Proxy calls to dl (#1686)
* Add tracer metrics to startup logs (#1689)
* New version of the WAF (#1692)
* Merging repos: 'dd-shared-components-dotnet' into 'dd-trace-dotnet'. (#1694)
* Sending more relevant data to backend (#1695)
* Prepare the `/shared` folder for consumption by the Profiler. (#1697)
* Don't cache the process instance for runtime metrics (#1698)
* Use AppDomain.CurrentDomain.BaseDirectory to get current directory for configuration (#1700)
* Don't trigger Tracer-specific CI for changes to shared assets not used by the Tracer (#1701)

## Fixes
* Add PreserveContext attribute for async integrations (#1702)

## Build / Test
* Add end-to-end integration tests for logs injection (#1637)
* Produce NuGet package to deploy automatic instrumentation (#1661)
* Adds Execution time benchmarks (#1687)
* Add fix for Samples.MultiDomainHost.App.NuGetHttpWithRedirects test application (#1691)
* Reduce snapshot file path length (#1696)

[Changes since 1.28.2](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.2...v1.28.5-prerelease)

## [Release 1.28.3-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.3-prerelease)

## Changes
* Adds Datadog.AutoInstrumentation.NativeLoader (#1577)
* The v0 version of App Sec (#1647)
* Read performance counter values from environment variables in AAS (#1651)
* Improve error handling for performance counters (#1652)
* Add instrumentation for Begin/EndGetResponse to WebRequest (#1658)
* Cache native AssemblyReference instances (#1665)
* Use link shortener for IIS permissions (#1666)
* Native profiler Initialize callback optimization (#1672)
* Update CI Visibility specification (#1682)

## Fixes
* Fixes native regex usage on non windows platforms. (#1662)

## Build / Test
* Merge auto-instrumentation code into Datadog.Trace.dll (#1443)
* replace "minimal" solution with a solution filter (#1631)
* Add support for a temporary NGEN pipeline (#1642)
* Small build improvements (#1646)
* Filter now applied to the samples when compiling (#1653)
* Crank native profiler fix (#1655)
* Reduce length of snapshot paths (#1657)
* Update GitHub action release workflows (#1659)
* Fixes crank pipeline on PR merge commits. (#1669)
* Disable CallSite scenario from Throughput tests. (#1674)
* Removes code for Callsite scenario from the throughput tests (#1679)
* Add a custom test framework to monitor execution of unit tests in ducktyping library (#1680)
* Add tests for changes to Datadog.Trace's Public API (#1681)

[Changes since 1.28.2](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.2...v1.28.3-prerelease)

## [Release 1.28.2](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.2)

## Changes
* Add additional logs injection fallback for NLog 1.x (#1614)
* Remove version from the `test.framework` tag in CIApp (#1622)
* Fix ReJIT and Shutdown log levels (#1624)
* Logger API refactoring (#1625)
* Add experimental NGEN support (#1628)
* Clear metadata when an appdomain unloads (#1630)
* Don't add the sampling priority header if empty (#1644, #1649)

## Fixes
* Fix CIApp in latest version of MSTest (#1604)
* Add fallback for logs injection for NLog versions < 4.1.0 (#1607)
* Fix PInvokes to the native lib on non windows os. (#1612)
* Add fix for log injection with log4net.Ext.Json versions < 2.0.9.1 (#1615)
* Fix CIApp Feature Tracking (#1616)
* Fix crank project reference (#1617)
* Fixes the dd-trace exit code always returning 0 (#1621)
* Preserve the task cancelled state when using calltarget instrumentation (#1634)
* Fix the native logger path issue (#1635)
* Add better error handling for the Header Tags feature accessing System.Web.HttpResponse.Headers (#1641)

## Build / Test
* Add analyzer project + ThreadAbortAnlayzer to detect inifinte loops on ThreadAbortException (#1325, #1619)
* Add ASP.NET Core on IIS Express tests (#1582)
* Split Windows regression tests and integration tests to save drive space (#1586)
* CIApp - Add support to add custom env-vars from dd-trace wrapper (#1594)
* Merge tools into Nuke (#1605)
* Make the benchmark step optional (#1608)
* Add GitHub workflows for automating Release creation (#1611)
* Fix throughtput/crank pipeline. (#1618)
* Fix ARM64 Integration tests (#1629)
* Exclude more vendored files from code coverage (#1632)
* Add additional scrubbing for stacktraces in snapshots (#1633)
* Fix throughput tests (#1650)

[Changes since 1.28.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.28.0...v1.28.2)

## [Release 1.28.1-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.1-prerelease)

## Changes
* Add a public ForceFlushAsync API on the Tracer (#1599)
* CIApp: Update Bitrise spec (#1593)

## Fixes
* Fix memory leak in native code (#1564, #1600)

## Build / Test
* Switch to Nuke consolidated pipeline (#1595, #1587, #1598)
* Add a custom test framework to monitor execution of unit tests (#1572)

[Changes since 1.28.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.29.1...v1.28.1-prerelease)

## [Release 1.28.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.0)

## Changes
* Add support for NestedTypes and GenericParameters to EnsureTypeVisibility (#1561)
* Add support for Microsoft.Data.SqlClient 3.* (#1579)
* Enable calltarget by default on .NET 4.6+ runtimes (#1542)
* Utilize lower-overhead CustomSerilogLogProvider for older LogContext.PushProperties API when LogContext.Push API is not present (#1560)
* Ducktype - Explicit interface method implementation support (#1555)
* NUnit integration improvements (#1533)
* Add automatic instrumentation for AWS SQS (#1471)
* Upgrade spdlog to 1.4 and move to Static lib version (#1507)
* Cosmos Db support (#1473)
* Add net5.0 support to dd-trace (#1516)
* Avoid the trace copy when flushing spans (#1502)
* CIApp: Update CircleCI spec (#1503)

## Fixes
* Initialize performance counters asynchronously (#1564)
* Use a UUID for runtime-id instead of container id (#1548)
* Revert the order in which the log providers are resolved (#1578)
* Fixes environment values in the logs on non windows platforms (#1581)
* Ducktyping - Replace "calli" call over generic methods (#1557)
* Fix spans flushing on Testing framework instrumentations (#1535)
* Adds CorLib detection for assembly ref (#1522)
* Modify GetReJITParameters callback method to never return E_FAIL HRESULT (#1517)
* Flush both span buffers in a FlushTracesAsync() call (#1514)
* CIApp: Correctness and stability fixes. (#1504)
* AgentWriter FlushTracesAsync changes (#1501)

## Build / Test
* Remove callsite benchmarks and set iteration time back to 2 seconds (#1511)
* Nuke build: overwrite files when copying trace home directory (#1567)
* Wait 10 more seconds on runtime metrics tests (#1566)
* Hide warnings for EOL .NET Core targets (#1569)
* Fix x86 builds in consolidated pipeline (#1563)
* Fix race condition in PerformanceCountersListenerTests (#1573)
* Update README (#1576)
* Reduce dependencies between build tools and helper projects (#1568)
* CI tweaks (#1570)
* Fix "PrepareRelease msi" command (#1583)
* Fix flaky Kafka test (#1585)
* Small ASP.NET Snapshot refactoring (#1580)
* Don't create an azure artifact (#1589)
* Change native format to custom style (#1553, #1544)
* Update ci provider extractor according to specs (#1554)
* Add small CI fix and docker optimization (#1551)
* Upgrade non windows native builds to C++17 (#1543)
* Fix Enable32Bit flag for IIS container (#1536)
* Add an OWIN test application to the integration-tests pipeline (#1525)
* Small build improvements (#1531, #1524)
* Downgrade the version of cmake used for MacOs builds (#1529)
* Fix xUnit 2.2.0 test flakyness (#1526)
* Fix CosomsDb tests and disable PR triggers for old pipelines (#1523)
* Fix Nuke pipeline and remove some unused sample projects (#1521)
* Convert GitLab package signing build to use Nuke targets (#1512)
* Enable ultimate pipeline for PRs (#1515)
* Delete project NugetProfilerVersionMismatch (#1520)
* Crank sample app profiler detector. (#1518)
* Fix race condition in runner pipeline (#1510)
* Include Nuke build project in the sln (#1508)
* Add Nuke build project and update consolidated pipeline (#1476)
* Cleanup project files (redux) (#1499)

[Changes since 1.27.1](https://github.com/DataDog/dd-trace-dotnet/compare/v1.27.1...v1.28.0)

## [Release 1.27.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.27.1)

## Fixes
* Fix possible crash condition in .NET Framework 4.5, 4.5.1, and 4.5.2 (#1528, #1539)

## Build
* Update build pipelines to run in release/* and hotfix/* branches where appropriate (#1545, #1546)

[Changes since 1.27.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.27.0...v1.27.1)

## [Release 1.27.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.27.0)

## Changes
* Add ARM64 support (.NET 5 Only) (#1449)
* Add MSMQ automatic instrumentation integration (#1463)
* Add Confluent.Kafka automatic instrumentation integration (#1444, #1492)
* Exclude well-known URLs from tracing, to avoid multiple top level spans in AAS (#1447)
* Optimize log injection with NLog and log4net (#1475, #1489)
* _dd.origin tag improvements for CIApp (#1481)
* Cache MessagePack Span tag keys in UTF8 (#1482)
* Add DuckInclude attribute (#1487)

## Fixes
* Handle exceptions in async integrations in Calltarget (#1458)
* Fix Call opcode in DuckTyping on struct targets (#1469)
* Refactor DisposeWithException invocations to remove null conditional operator (#1493)
* Add runtime-id tag to metrics to improve Fargate support (#1496)
* Remove the static collection in the HTTP module (#1498)

## Build / Test
* Remove stale AAS tests (#1466, #1467)
* Add Windows container sample (#1472)
* Add tests for IIS classic mode (#1462)
* Add CIApp test framework integrations test suite (#1317)
* Add and enforce copyright headers (#1445, #1485)
* Clean up project files (#1464, #1468)
* Add GH Action to auto-create benchmark branch (#1483)
* Add throughput test for ARM64 (#1490)
* Convert ASP.NET tests to use Snapshot Testing with VerifyTests/Verify
* Fix .vcxproj file for latest msbuild version. (#1495)
* Remove a few benchmarks to make the pipeline faster (#1497)
* Increase crank duration to 4 minutes (#1505)

[Changes since 1.26.3](https://github.com/DataDog/dd-trace-dotnet/compare/v1.26.3...v1.27.0)

## [Release 1.26.3](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.26.3)

## Fixes
* Fix crash in the ASP.NET integration when running in IIS in classic mode (#1459)
* Fixes dynamically emitted methods signatures (#1455, fixes #1232)


## Build / Test
* Add benchmarks for log4net and nlog (#1453)
* Update CoreCLR headers from dotnet/runtime v5.0.5 tag (#1451)
* Adds the FeatureTracking tool and CIApp implementation (#1268)

[Changes since 1.26.2](https://github.com/DataDog/dd-trace-dotnet/compare/v1.26.2...v1.26.3)

## [Release 1.26.2](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.26.2)

## Changes
* Reduce overhead when using log injection (`DD_LOGS_INJECTION`)  with Serilog (#1435, #1450)
* Use the profiler API instead of the IIS configuration to register the ASP.NET integration (#1280)
* Various optimizations (#1420, #1425, #1434, #1437, #1448)
* Allow calltarget instrumentation of nested classes (#1409)
* Add debug logs to help diagnose partial flush issues (#1432)
* Add execution time logs for native callbacks (#1426)
* Upgrade LibLog to 5.0.8 (#1396)


## Fixes
* Remove obsolete "Using eager agent writer" warning at startup (#1441)
* Fix wrong service name when a DbCommand implementation is named "Command" (#1430, fixes #1282)


## Build / Test
* Run calltarget integration tests only with inlining (#1439, #1452)
* Clean up the PrepareRelease tool (#1442)
* Stop using external domains in integration tests (#1438)
* Prevent dependabot from opening PR's against the Microsoft.Build.Framework NuGet package (#1427)
* Remove useless dependency from benchmark project (#1428)
* Fix a build issue with the MSI (#1423)

[Changes since 1.26.1](https://github.com/DataDog/dd-trace-dotnet/compare/v1.26.1...v1.26.2)

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
