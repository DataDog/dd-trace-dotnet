# Datadog .NET Tracer (`dd-trace-dotnet`) Release Notes







## [Release 3.3.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v3.3.0)

## Summary

- [ASM] Fix some minor bugs (#5943, #5955, #6017)
- [ASM] Improvements to stack trace reporting (#6011, #5997)
- [Dynamic Instrumentation] Add support for `typeof` expression
- [Continuous Profiler] Add support for heuristic-based activation (#5240, #6002, #6026)
- [DBM] Full propagation mode for SQL Server (#5859)

## Changes

### ASM
* [ASM] Avoid unhandled HttpRequestValidationExceptions (#5943)
* [ASM] Avoid reporting unknown matcher WAF errors (#5955)
* [ASM] RASP: add telemetry tag for shell injection (#5993)
* [ASM] Add new capabilities for RC (#6008)
* [ASM] Change stack trim proportion (#6011)
* [ASM]Fix InvalidOperationException in httpContext.Items (#6017)
* [ASM] Capabilities reporting against WAF versions. (#6028)
* [IAST] Add Stack trace to vuln location (#5997)
* [IAST] Fix system test weak_cipher system test (#6034)

### Continuous Profiler
* [Profiler] Support Single Step Instrumentation deployment and activation (#5240)
* [Profiler] Contention profiling: add blocking thread name (#5981)
* [Profiler] Fix duplicated lifecycle telemetry (#6002)
* [Tool] update continuous profiler diagnostics (#6014)
* [Profiler] Disable `timer_create`-based CPU profiler when required (#6015)
* [Profiler] Send ssi info with profiles (#6026)

### Debugger
* [Dynamic Instrumentation] DEBUG-2323 Add support for `typeof` expression in EL (#5539)

### Build / Test
* [Profiler] Disable SSI telemetry by default (#6020)
* Fix SSI tests for profiler integration tests (#6016)
* [Profiler/CI] Disable Profiler Windows ASAN job (#5987)
* Add explicit permissions to all workflows (#5728)
* [Test Package Versions Bump] Updating package versions (#5873)
* [build] Build tracer with ReadyToRun (#5962)
* Display the crash tests stdout live (#5964)
* Timeit bump and fixes (#5971)
* Add `linux-musl-arm64` standalone `dd-trace` to the v3 release artifacts (#5974)
* [CONTSEC-1501] Comment the action that uploads SARIF to Datadog (#5977)
* Include snapshot diff in snapshot body (#5988)
* Make sure we run all the TFMs on master builds (#5990)
* Minor CI fixes (#6000)
* Fix installer tests (#5994 => main) (#6004)
* Fix the trace pipeline stage (#6007)
* [Build] Update and fix linux debug symbols artifact (#6009)
* Fix bug in version bump task (#6022)
* [CI] Shorten too long snapshot file names (#6024)
* Fix gitlab build (#6025)
* [BUILD] Fix merge conflict (#6033)

### Miscellaneous
* DSM Full propagation mode for SQL Server (#5859)
* [Crashtracking] Fix the handling of COMPlus_DbgMiniDumpName (#5980)

[Changes since 3.2.0](https://github.com/DataDog/dd-trace-dotnet/compare/v3.2.0...v3.3.0)

## [Release 3.2.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v3.2.0)

## Summary

This is the first stable release of the next major version of the .NET APM SDK. 

The following are the high-level changes present in the 3.x.x release line compared to 2.x.x. These include breaking changes in public APIs, changes in artifacts, and changes to default settings. 

For the full list of changes, including exactly what changed and how you should handle them, please see the [MIGRATING](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/MIGRATING.md) document

### New Features
- **Support for alpine images on ARM64**. `alpine` images with version `3.18` and above, running on ARM64 images are now supported on .NET 6+.

### Breaking changes
- **Custom-only tracing (using the _Datadog.Trace_ NuGet package), _without_ any automatic tracing, is no longer supported**. Custom instrumentation with the  _Datadog.Trace_ NuGet where you have _also_ configured [automatic-instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/) is still supported as it was in v2.x.x.
- **The public API surface has changed** in the *Datadog.Trace* NuGet package. A number of previously obsolete APIs have been removed, and some other APIs have been marked obsolete. Most changes are related to how you create `TracerSettings`  and `Tracer` instances.
- **Changes to default settings**. The default values of some settings have changed, and others have been removed. See below for more details.
- **Changes in behavior**. The semantic requirements and meaning of some settings have changed, as have some of the tags added to traces.  See below for more details.
- **The 32-bit MSI installer will no longer be available**. The 64-bit MSI installer already includes support for tracing 32-bit processes, so you should use this installer instead. 
- **The client library will still be injected when `DD_TRACE_ENABLED=0`**. In v2.x.x, setting `DD_TRACE_ENABLED=0` would prevent the client library from being injected into the application completely. In v3.0.0+, the client library will still be injected, but tracing will be disabled.
- **Referencing the `Datadog.Trace.AspNet` module is no longer supported**. In v1.x.x and 2.x.x ASP.NET support allowed adding a reference to the `Datadog.Trace.AspNet` module in your web.config. This is no longer supported in v3.x.x.

### Deprecation notices
- **.NET Core 2.1 is marked EOL** in v3.0.0+ of the tracer. That means versions 2.0, 2.1, 2.2 and 3.0 of .NET Core are now EOL. These versions may still work with v3.0.0+, but they will no longer receive significant testing and you will receive limited support for issues arising with EOL versions.
- **Datadog.Trace.OpenTracing is now obsolete**. OpenTracing is considered deprecated, and so _Datadog.Trace.OpenTracing_ is considered deprecated. See the following details on future deprecation.
- **macOS 11 is no longer supported for CI Visibility** in v3.0.0+. Only macOS 12 and above are supported.

### Major version policy and future deprecation
- **Announcing a major version roadmap**. We intend to make yearly major releases, starting from v3.0.0 in 2024, and v4.0.0 in 2025. We will aim for minimal breaking changes, with the primary focus being on maintaining support for new versions of .NET and removal of EOL frameworks and operating systems.
- **Planned removal of support for .NET Core 2.x and .NET Core 3.0** in version v4.0.0+. We intend to completely remove support for .NET Core 2.x and .NET Core 3.0 in v4.0.0. .NET Framework 4.6.1+ will continue to be supported.
- **Planned removal of support for some linux distributions**. In version v4.0.0, we intend to drop support for CentOS 7, RHEL 7, and CentOS Stream 8.
- **Planned remove of support for App Analytics**. In version v4.0.0, we intend to drop support for App Analytics and associated settings.

For the full list of changes, including exactly what changed and how you should handle them, please see the [MIGRATING](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/MIGRATING.md) document

## Updates in this release

In addition to the changes described above, this release includes the following features:

* [Tracer] Skip inserting the startup hook into methods in the type `Costura.AssemblyLoader` (#5910)
* [Tracer] Fix bug in ADO.NET connection string extraction (#5949)
* [Exception Replay] Update configuration values (#5821)
* [IAST] Taint values coming from database (#5804)
* [IAST] Allow customized cookie filtering (#5804)
* [ASM] RASP shell injection vulnerability (#5871)
* [Profiler] Provide the thread id that blocked another thread (#5959)

## Changes

### Tracer
* [Tracer] Skip inserting the startup hook into methods in the type `Costura.AssemblyLoader` (#5910)
* Add support for alpine on arm64  (#5933)
* Fix incorrect instrumentation for `new TracerSettings(bool)` (#5949)
* Protect the connection string tags extractor from an invalid connection string (#5956)

### CI Visibility
* Don't include the Datadog.Trace.BenchmarkDotNet NuGet in the release artifacts (#5954)

### ASM
* [IAST] Taint values coming from database (#5804)
* [ASM] RASP shell injection vulnerability (#5871)
* [IAST] CallSite with generics support (#5913)
* [IAST] Move analyzers init to an explicit call (#5920)
* [IAST] Taint db minor fixes (#5926)
* [IAST] Support for specifying aspect min version (#5931)
* [IAST] Cookie filter implementation (#5947)

### Continuous Profiler
* [Profiler] Provide the thread id that blocked another thread (#5959)
* [Profiler] Improve .NET Framework profiling support (#5867)

### Debugger
* [Exception Replay] Update configuration and add test suite for ASP.NET Core (#5821)

### Serverless
* [Mini Agent][Private Beta Testing] Mini-agent for Azure Function Apps for non-consumption plans (#5792)
* [serverless] No-op AWS Lambda integration on missing API Key (#5900)
* Revert "[serverless] No-op AWS Lambda integration on missing API Key" (#5941)

### Build / Test
* [Profiler] Fix `LinuxDlIteratePhdrDeadlock` test (#5963)
* Output samples to a single top-level "artifacts" folder  (#5744)
* Try to head off future build issues (#5770)
* Fix bug in verification stage of release (#5894)
* Need to freeze/unfreeze all PRs (#5902)
* Replace fpm with nfpm (#5905)
* Ignore `StyleCop.Analyzers` in dependabot (#5906)
* Fix gitlab build (#5907)
* Remove prerelease flag from smoke tests (#5912)
* Enable ad-hoc memory dumps on Windows x86 (#5919)
* Stop testing .NET Core 2.1 on PRs (#5922)
* Try fix single step download builds (#5923)
* Remove obsolete lib-injection build artifacts (#5927)
* Remove dependency of download-single-step-artifacts on build (#5945)
* Ensure we clean log files before testing with Nuke (#5950)
* Log to a random file in telemetry forwarder tests (#5951)
* Remove the xml and pdb files from the linux packages (#5961)
* Fix debugger arm64 alpine tests (#5965)
* [IAST] Added missing netstd snapshot (#5966)

### Miscellaneous
* Normalize the environment variable names used by crashtracking (#5898)
* Pin `StyleCop.Analzyers` to latest pre-release (#5908)
* Fix signature size check in ModifyLocalSig (#5921)
* Use a native logger for critical failures in the loader (#5929)
* Fix ToString and ToWString on large strings (#5930)
* Prevent the native loader from being unloaded while sending telemetry (#5944)
* [Crashtracking] Keep mangled name in case of error (#5952)

[Changes since 2.58.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.58.0...v3.2.0)


## [Release 3.1.0-prerelease](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v3.1.0-prerelease)

## Summary

This is the second pre-release of the next major version of the .NET APM SDK. 

- [ASM] Changes to the collection of usr.id for authenticated clients
- [ASM] IAST Email HTML Injection vulnerability
- [Dynamic Instrumentation] Support nullable types in templates and string lexicographic comparison
- [Dynamic Instrumentation] SymDb readiness for Open Beta, matching symbols based on signature
- [Exception Replay] Normalized exception hashing for more fine-grained aggregation

In addition, the following are the high-level changes present in the 3.x.x release line compared to 2.x.x. These include breaking changes in public APIs, changes in artifacts, and changes to default settings. 

For the full list of changes, including exactly what changed and how you should handle them, please see the [MIGRATING](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/MIGRATING.md) document

### Breaking changes
- **Custom-only tracing (using the _Datadog.Trace_ NuGet package), _without_ any automatic tracing, is no longer supported**. Custom instrumentation with the  _Datadog.Trace_ NuGet where you have _also_ configured [automatic-instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/) is still supported as it was in v2.x.x.
- **The public API surface has changed** in the *Datadog.Trace* NuGet package. A number of previously obsolete APIs have been removed, and some other APIs have been marked obsolete. Most changes are related to how you create `TracerSettings`  and `Tracer` instances.
- **Changes to default settings**. The default values of some settings have changed, and others have been removed. See below for more details.
- **Changes in behavior**. The semantic requirements and meaning of some settings have changed, as have some of the tags added to traces.  See below for more details.
- **The 32-bit MSI installer will no longer be available**. The 64-bit MSI installer already includes support for tracing 32-bit processes, so you should use this installer instead. 
- **The client library will still be injected when `DD_TRACE_ENABLED=0`**. In v2.x.x, setting `DD_TRACE_ENABLED=0` would prevent the client library from being injected into the application completely. In v3.0.0+, the client library will still be injected, but tracing will be disabled.
- **Referencing the `Datadog.Trace.AspNet` module is no longer supported**. In v1.x.x and 2.x.x ASP.NET support allowed adding a reference to the `Datadog.Trace.AspNet` module in your web.config. This is no longer supported in v3.x.x.

### Deprecation notices
- **.NET Core 2.1 is marked EOL** in v3.0.0+ of the tracer. That means versions 2.0, 2.1, 2.2 and 3.0 of .NET Core are now EOL. These versions may still work with v3.0.0+, but they will no longer receive significant testing and you will receive limited support for issues arising with EOL versions.
- **Datadog.Trace.OpenTracing is now obsolete**. OpenTracing is considered deprecated, and so _Datadog.Trace.OpenTracing_ is considered deprecated. See the following details on future deprecation.
- **macOS 11 is no longer supported for CI Visibility** in v3.0.0+. Only macOS 12 and above are supported.

### Major version policy and future deprecation
- **Announcing a major version roadmap**. We intend to make yearly major releases, starting from v3.0.0 in 2024, and v4.0.0 in 2025. We clearly will aim for minimal breaking changes, with the primary focus being on maintaining support for new versions of .NET and removal of EOL frameworks and operating systems.
- **Planned removal of support for .NET Core 2.x and .NET Core 3.0** in version v4.0.0+. We intend to completely remove support for .NET Core 2.x and .NET Core 3.0 in v4.0.0. .NET Framework 4.6.1+ will continue to be supported.
- **Planned removal of support for some linux distributions**. In version v4.0.0, we intend to drop support for CentOS 7, RHEL 7, and CentOS Stream 8.
- **Planned remove of support for App Analytics**. In version v4.0.0, we intend to drop support for App Analytics and associated settings.

For the full list of changes, including exactly what changed and how you should handle them, please see the [MIGRATING](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/MIGRATING.md) document


## Changes

### Tracer
* Fix `NullReferenceException` in ASP.NET Core when `RoutePattern.RawText` is `null` (#5880)
* Fix `NullReferenceException` in `HttpClientResponse.GetCharsetEncoding` (#5881)
* Disable keep-alive in HttpClientRequestFactory (#5810)
* Fix error checking for CallTargetBubbleUpException (#5836)
* Ensure top-level entry points are wrapped with try-catch (#5838)
* Add an `IsManualInstrumentationOnly` flag to Datadog.Trace.Manual (#5866)

### ASM
* [ASM] Changes to the collection of usr.id for authenticated clients (#5738)
* [ASM] IAST Email HTML Injection vulnerability (#5780)
* [ASM] Upgrade WAF to version 1.19.1 (#5820)
* [ASM] Add RASP timeout flag (#5827)
* [IAST] Safeguard Insert Before / After aspects with try/catch (#5839)
* [IAST] Safeguard Method Replace aspects with try/catch (#5841)
* [ASM] Detect enabled RASP rules (#5846)
* [ASM] Disable email Injection instrumented tests (#5875)
* [ASM] ensure struct is on the stack before passing to native code (#5882)
* [IAST] Broaden AspNet cookies filtering (#5830)
* [ASM] Refactor hardcoded secret analyzer (#5883)

### Continuous Profiler
* [Profiler] LibrariesInfoCache: fix reload bug (#5837)
* [Profiler] Add Callstack::CopyFrom method (#5842)
* [Profiler] Fix null named thread (#5851)

### Debugger
* [Dynamic Instrumentation] DEBUG-2489 Add default 3rd party detection includes\excludes (#5722)
* [Dynamic Instrumentation] DEBUG-2664 Remove `this` from static methods arguments upload (#5833)
* [Dynamic Instrumentation] DEBUG-2216 Getting value of field or property throws `NotSupportedException` (#5558)
* [Dynamic Instrumentation] DEBUG-2365 Support string lexicographic comparison (#5538)
* [Dynamic Instrumentation] DEBUG-2088 Support nullable types in templates (#5543)
* [Dynamic Instrumentation] DEBUG-2560 EL- Fix `IsEmpty` for string and collections (#5809)
* [Dynamic Instrumentation] DEBUG-2524 Fix EL numeric binary operations (#5815)
* [Dynamic Instrumentation] Improved instrumentation matching of symbols received through SymDb (#5829)
* [Exception Replay] Normalized exception hashing for more fine-grained aggregation (#5872)

### Build / Test
* [Samples] Update IIS sample Dockerfile (#5805)
* Update `config_norm_rules` with old DI config (#5816)
* Simplify determining whether it's a debug run or not (#5817)
* Use unified Gitlab pipeline for APM SDKs for SSI artifacts (#5818)
* [Test Package Versions Bump] Updating package versions (#5819)
* Fix builds on release/2.x (#5826 -> master) (#5828)
* Add a scheduled job that sets the SSI variables in all tests (#5832)
* Add Callsite aspects analyzer to check for "safe" patterns (#5835)
* Catch exceptions when trying to shutdown IIS (#5840)
* [Test Package Versions Bump] Updating package versions (#5845)
* [Dynamimc Instrumentation] Update debugger .slnf file (#5858)
* Skip the mass transit test to see if it solves flake issues (#5861)
* Add verification step to create_draft_release to check SSI one-pipeline succeeded (#5865)
* [build] change agent image source (#5874)
* Try fix smoke tests (#5889)
* * [Dynamic Instrumentation] Fix broken debugger integration test (#5869)

### Miscellaneous
* [IAST] Add a mark to the modified instructions in IL dumps (#5854)
* Update Datadog.Trace README to reference v3 migration guide (#5857)
* Config refactor - Add telemetry to otel config (#5717)
* Exclude an SSIS service from auto-tracing (#5813)
* [CrashTracking] Ensure crashtracking does not prevent coredump collection (#5852)

[Changes since 2.56.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.56.0...v3.1.0-prerelease)


## [Release 2.56.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.56.0)

## Summary

- [Tracing] Fix mapping of `http.status_code` OpenTelemetry tag
- [Tracing] Fix bug where runtime metrics turns a recoverable OOM into an unrecoverable crash
- [Dynamic Instrumentation] Improve exception replay capturing accuracy
- [Dynamic Instrumentation] Fix potential crash related to exception replay

## Changes

### Tracer
* Map `http.status_code` to meta dictionary (#5782)
* Record when we use v2 instrumentation with a v3 version of the manual tracer (#5791)
* Improve handling of OOM (#5797)

### Debugger
* [Exception Replay] Improved exceptions capturing accuracy + fixed a crash caused by mishandling of exception case probe statuses (#5798)

### Miscellaneous
* Tracer flare - Inspect the AGENT_CONFIG content to set the log level (#5802)

### Build / Test
* [Test Package Versions Bump] Updating package versions (#5776)
* [Test Package Versions Bump] Updating package versions (#5796)
* Include a version.txt and index.txt in the uploaded Azure assets (#5794)
* Build OCI images for every branch (#5800)
* Package musl assets in linux glibc tar folder (#5801)


[Changes since 2.55.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.55.0...v2.56.0)


## [Release 2.55.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.55.0)

## Summary

- [Dynamic Instrumentation] Fixed rewriting issues stemmed from combination of multiple instrumentations
- [Tracing] dd support for MySql.Data 9.0.0
- [Tracing] Handle unknown service names for OpenTelemetry/Activities
- [ASM] Standalone Billing

## Changes

### Tracer
* Add support for MySql.Data 9.0 (#5786)
* Correctly set `activityKey` when we change the Trace ID (#5771)
* Change unknown_service to DefaultServiceName (#5671)

### ASM
* [ASM] Standalone Billing (#5565)
* [ASM] Standalone Billing (part 2: Propagation) (#5743)
* [ASM] Appsec events in meta struct (#5779)

### Continuous Profiler
* [Profiler] Support "auto" for profiler enablement (#5766)
* [Profiler] Fix bug when create thread lifetime event (#5769)

### Debugger
* [Dynamic Instrumentation] Fixed Instrumentation failures revealed by the Exploration Tests of Line & Method probes (#5784)
* [Dynamic Instrumentation] Fixed SymDB upload when PDB is absent (#5789)
* [Exception Replay] Mitigating an exception thrown while processing the methods participating in exception stack traces (#5783)

### Miscellaneous
* [SSI] Bail out on known-faulty .NET 6 version (#5761)

### Build / Test
* Integration test for Oracle (#5607)
* Skip flaky `ActivityTests` on macos (#5763)
* Fix macOS cmake warnings/errors (#5788)


[Changes since 2.54.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.54.0...v2.55.0)


## [Release 2.54.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.54.0)

## Summary

* [ASM] Exploit prevention: Support SQL Injection attacks
* [Profiler] Add `timer_create`-based CPU profiling on Linux
* Multiple fixes

## Changes

### Tracer
* [Tracer] Rejit refactor to support multiple products in the same method (#5533)
* [Tracer] Move definitions to native side (#5592)
* [Remote Configuration] Buffer the response and include in error (#5675)
* [Dynamic Instrumentation] DEBUG-2356 3rd party detection Include\exclude for SymDB (#5681)
* [TRACER] Ensure synchronization in MethodInfo lazy operations (#5698)
* Config refactor - distinguish between "not present" and "not valid" (#5713)
* Config refactor - support "converters" for other configuration types (#5714)
* Config refactor - fix struct/class `T` nullable ref issues, and allow "raw" access to `ConfigurationResult` (#5715)
* Config refactor - remove duplication and OTel-specific code from `ConfigurationBuilder` (#5716)
* [Tracing] fix precedence of remote vs local sampling rules (#5720)
* [Tracer] Extracting Last Parent Id If Conflicting SpanIds are Found with The W3C Headers (#5721)
* Fix flake in `SimpleActivitiesAndSpansTest` (#5735)
* [Dynamic Instrumentation] Fix SymDB config keys to match RFC (#5737)
* Do not reset `Activity.Id` for non-W3C formats (#5739)
* Fix `ContentEncoding` in `IApiResponse` (#5748)

### CI Visibility
* Update CI Visibility metrics to latest requirements (#5747)

### ASM
* [ASM] Exploit prevention: Support SQL Injection attacks (#5651)
* [ASM] Fix WAF timeout false positives (#5724)
* [ASM] Capture ObjectDisposedException (#5732)
* [ASM][IAST] Add support for CallSites in functions with by ref value type arguments (#5755)
* [ASM] Iast/Rasp vulnerability manager (#5764)
* [ASM] Fix - InvalidOperationException/ConcurrentOperationsNotSupported (#5765)

### Continuous Profiler
* [Profiler] Add `timer_create`-based CPU profiling on Linux (#5476)
* [Profiler] Add custom dl_iterate_phdr and use it in libunwind (#5660)
* [Profiler] Add heap size metrics (gen2, loh and poh) (#5669)
* [Profiler] Add global flag to prevent the profiler from stackwalking while the app is crashing (#5729)

### Debugger
* [Dynamic Instrumentation] Fixed instrumentation error (InvalidProgramException) related to EH clauses (#5774)

### Build / Test
* Update Windows hosted image to latest software (#5416)
* Build native test binaries in build jobs (#5614)
* Run smoke tests as though they're in SSI (#5673)
* Enforce not referencing Datadog.Trace directly in sample projects (#5683)
* [builds] fix `build_in_docker` scripts (#5688)
* [Tracing] [Samples] Update MicrosoftExtensionsExample.csproj to remove incompatible library (#5689)
* Update codeowners to make Directory.Build.props to make them universal (#5703)
* Build against `macos-12` instead of `macos-11` (#5707)
* Set CODEOWNERS of debugger configuration keys to the debugger team (#5709)
* Add basic smoke tests for macos (#5710)
* Update workflow file again (#5712)
* Try to fix the build by changing BuildId (#5718)
* Fix Gitlab build and codeCoverage bugs (#5723)
* Fix some macOS build issues (#5725)
* Better logs folder creation in chiseled smoke test (#5731)
* Add workflow monitor to all workflows (#5733)
* Minor github action changes (#5734)
* Revert "Add workflow monitor to all workflows (#5733)" (#5741)
* Make BuildTracerHome build the native profiler (#5750)
* Skip flaky tests on .NET Core 2.1 (#5753)
* Add attribute for skipping tests in CI without using `[Fact(Skip = "")]` (#5756)
* Re-order integrations folder in CODEOWNERS (#5762)
* Stop testing with a specific build in macos smoke tests (#5772)
* Filter out .NET Core 3.1 NuGet tests in prerelease versions (#5773)
* Switch system-tests to python 3.12 (#5777)

### Miscellaneous
* [Test Package Versions Bump] Updating package versions (#5639)
* Single-step guard rails: Use stdin instead of args to invoke telemetry (#5677)
* [Test Package Versions Bump] Updating package versions (#5699)
* [Test Package Versions Bump] Updating package versions (#5727)
* [Test Package Versions Bump] Updating package versions (#5752)
* Lock access to rejitters (#5757)
* Revert native changes that moved the definitions to the native side (#5768)


[Changes since 2.53.2](https://github.com/DataDog/dd-trace-dotnet/compare/v2.53.2...v2.54.0)


## [Release 2.53.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.53.0)

## Summary

* [Tracing] Add support for ActivityLinks/OTEL Span Links
* [Tracing] Add support for OpenTelemetry's AddEvent and RecordException API's, and environment variables
* [Tracing] Add support for Serilog v4 and NLog 5.3.0
* [CI Visibility] Add support for Microsoft.CodeCoverage
* [CI Visibility] Add support for UDS and NamedPipes
* [ASM] Enable Runtime Application Self-Protection (RASP) by default
* [Dynamic Instrumentation] Support using Boolean literals in the expression language
* [DBM] Add injection support for Oracle queries (service mode only)

## Changes

### Tracer
* [Tracer] Support remote config for `DD_TRACE_SAMPLING_RULES` and Adaptive Sampling (#5453)
* [Tracer] Finishes Adding Support for ActivityLink (#5627)
* [Tracing] Add standardized support for OpenTelemetry AddEvent and RecordException API's (#5630)
* Add support for Serilog v4 (#5649)
* Remap `http.response.status_code` to `http.status_code` (#5654)
* Exclude vsdbg from tracing (#5657)
* [Tracing] Adds support for mapping stable OpenTelemetry environment variables to their Datadog equivalents (#5661)
* Don't allocate very large buffers when deserializing responses (#5665)
* [Tracer] SpanLinks Permissive null Clean Up (#5674)

### CI Visibility
* [CI Visibility] Automatic reporting of Microsoft.CodeCoverage percentage (#5633)
* [CI Visibility] UDS and NamedPipes support (#5634)

### ASM
* [ASM] Update WAF log messages (#5571)
* [ASM] Improved Unsafe Encoder readability  (#5587)
* [ASM] Add the span id to the RASP events (#5588)
* [ASM] Add test for null response in aspnet core (#5590)
* [ASM] RASP: Add stack trace bottom and top filtering (#5621)
* [ASM] Enable RASP by default (#5625)
* [ASM] Capture exception to avoid errors (#5662)
* [ASM] Fix CloseLibrary condition (#5667)
* [ASM][IAST] Add `_dd.iast.json.tag.size.exceeded` telemetry metric (#5641)

### Continuous Profiler
* [Profiler] Fix services start/stop (#5616)
* [Profiler] Few missing changes for IService startup/stop (#5619)
* [Profiler] Improve configuration aroung SSI/non-SSI (#5620)
* Add a space before |fg: in case of an unknown type (#5624)
* Normalize the profiler thread names (#5626)
* [Profiler] Investigate and fix profiler benchmarks failures (#5631)

### Debugger
* [Dynamic Instrumentation] Aligned the line probe snapshot to fix System Tests failures (#5628)
* [Dynamic Instrumentation] Check specific path for diagnostics upload (#5461)
* [Dynamic Instrumentation] Cleansing third party module names to avoid conflicts with customer's modules (#5622)

### Fixes
* Fix RCM Capabilities bugs (#5606)
* Fix NLog direct log shipping 5.3+ when no config is present (#5609)

### Miscellaneous
* [DBM] add injection support for oracle queries (but only service mode) (#5506)
* [CrashTracking] Check if native crashes are caused by Datadog (#5573)
* Make crash tracking opt-out (#5582)
* [Test Package Versions Bump] Updating package versions (#5605)
* Fix NLog direct log shipping 5.3+ when no config is present (#5609)
* Record SSI injection values in configuration (#5611)
* Single-step guard rails: Move version.h to shared code (#5635)
* Single-step guard rails: Update `RuntimeInformation` to include "inferred" runtime version (#5636)
* Single-step guard rails: telemetry (#5637)
* Normalize the tracer thread names (#5644)
* Update libdatadog to v10 (#5653)
* [CrashTracking] Let libdatadog set the endpoint (#5666)
* [Crashtracking] Mark DD_* threads as suspicious (#5647)

### Build / Test
* Display C++ static analysis errors in the CI output (#5608)
* chore(lib-injection): update base image to alpine 3.20 (#5613)
* Set LD_PRELOAD in integration tests (#5617)
* [SINT-1401] update windows code signer to v0.2.3 (#5629)
* Ensure we catch exceptions in named pipe mock agent (#5646)
* Fix broken tests that only run on main (#5670)
* chore(serverless): update `CODEOWNERS` (#5672)
* [Profiler/Tracer] Bump FluentAssertions to 6.12.0 (#5599)
* [Profiler] Capture dump on timeout in wrapper tests (#5602)
* [Profiler] Fix race condition in SocketTimeout (#5648)


[Changes since 2.52.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.52.0...v2.53.0)


## [Release 2.52.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.52.0)

## Summary

* [CIVisibility] Fixes for `test.command` tag value and version-conflict scenarios
* [ASM] Adds support for session-timeout vulnerability
* [ASM] Adds support for Grpc.AspNetCore source tainting and exclusion marks
* [Tracing] Single step instrumentation no longer instruments EOL runtimes by default

## Changes

### Tracer
* [Tracer] Delay sampling decisions (#5306)
* [Tracer] Only set `_dd.agent_psr` tag when using sampling rates from Agent (#5545)
* Refactor `LifetimeManager` callbacks for `UnhandledException` (#5557)

### CI Visibility
* [CIVisibility] Add SourceLink support to CIEnvironmentValues (#5535)
* [CI Visibility] IPC subsystem (#5537)
* [CI Visibility] - Close and flush incomplete TSLV objects before exiting (#5549)
* [CI Visibility] Send data to the test session using IPC (#5551)
* [CIVisibility] Fix test.command tag value (#5560)
* [CIVisibility] Add a code coverage injection session tag (#5561)
* [CIVisibility] Tag profiles created by BenchmarkDotnet integration and add the IgnoreProfileAttribute (#5568)
* [CIVisibility] Fix support for custom spans with version mismatch (#5578)

### ASM
* [ASM][IAST] Add source tainting for Grpc dotnet (#5473)
* [ASM] Rasp sinks instrumentation (#5512)
* [ASM][IAST] Add vulnerability marks to Range + Add the mark to Escaped XSS (#5531)
* [ASM] RASP span metrics (#5542)
* [ASM] Upgrade WAF to version 1.18 (#5546)
* [ASM] Remove the need to pass httpcontext to SecurityCoordinators (#5548)
* [ASM][IAST] Session Timeout vulnerability (#5559)
* [ASM][IAST] Fix System.Text.Json: Properly implement GetRawText() aspect (#5572)
* [ASM] Fix null ref (#5596)

### Miscellaneous
* Bail out of instrumentation in SSI when an unsupported platform is detected (#5524)
* Don't instrument most `dotnet` SDK calls (#5564)
* [Test Package Versions Bump] Updating package versions (#5569)
* [Test Package Versions Bump] Updating package versions (#5585)
* Crash tracking (#5451)

### Build / Test
* Update `CODEOWNERS` for APM SDK IDM ownership (#5525)
* [All Natives] Fix C5105 Warning for Windows Native Projects (#5541)
* Update lib-injection docker image tags (#5544)
* Try fixing flake in `TelemetryControllerShouldUpdateGitMetadataWithTelemetry` on macOS (#5547)
* "Manually" install MSI dependencies instead of using choco install (#5553)
* Update the .DotSettings in `_build` (#5555)
* Remove span count assertions from Azure Function tests (#5556)
* [Tracer] scrub `_dd.agent_psr` from test snapshots (#5562)
* If a lot of snapshots have changed, we should check them all (#5563)
* [Build] Fix chiseled smoke tests jobs + capture coredump when crashing in all smoke tests jobs (#5570)
* Failing serverless tests are not reported in CI (#5574)
* [Tests] add null checks in `VerifyHelper` (#5575)
* Fix dependabot and improve `GeneratePackageVersions` (#5579)
* Disable automatic flush in AgentWriterTests.FaultyApi (#5581)
* Improve lib-injection tagging script (#5598)
* Bump Fody from 6.8.0 to 6.8.1 in /tracer/src/Datadog.Trace (#5576)
* [Tracer][Tests] scrub `_dd.agent_psr` tag from `TraceAnnotationsTests` snapshots only (#5591)


[Changes since 2.51.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.51.0...v2.52.0)


## [Release 2.51.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.51.0)

## Summary

- [APM] Fix rare `TypeLoadException` when running instrumented code
- [APM] Fix issue with runtime metrics that could cause memory usage to be reported as negative on x86
- [IAST] Directory listing leak vulnerability detection (Kestrel)
- [ASM] RASP: SSRF blocking. LFi reporting.
- [CI Visibility] Early Flake Detection
- [CI Visibility]  Selenium + RUM support
- [Dynamic Instrumentation] Support pointer and pinned local
- [Profiler] Several fixes

## Changes

### Tracer
* Encode the last seen Datadog span ID within `tracestate` (#5176)
* Check to ignore Activity when creating TraceID (#5318)
* read variable responsible for enabling SCA (#5432)
* Fix sending `Content-Length: 0` when using chunked-encoding (#5445)
* Add full support for multipart form requests (#5448)
* Avoid batching updates in runtime metrics (#5469)
* Update `RegexBuilder` to accept a `Timeout` so we can reduce flake (#5471)
* [Tracing] sampling code cleanup (#5477)
* Switch memory mapped counters to unsigned (#5480)
* [Dynamic Instrumentation] Add 'connectionString' to the list of redacted values (#5487)
* [Dynamic Instrumentation] Do not create probe processor and rate limiting for unbound line probes (#5503)
* [Dynamic Instrumentation] Consolidate PII redaction keys for all libraries. (#5522)

### CI Visibility
* [CI Visibility] Early Flake Detection (#5320)
* [CI Visibility] - Improvements to the `dd-trace ci ...` commands (#5468)
* [CI Visibility] - Selenium + RUM support (#5505)
* [CI Visibility] - Ensure continuous profiler flush on CI Visibility close method (#5513)
* [CI Visibility] - Fix force Evp proxy environment variable (#5526)
* [CI Visibility] Add linux `whereis` command as a fallback to locate the target binary. (#5532)

### ASM
* [ASM][IAST] Add Directory listing leak vulnerability (kestrel) (#5475)
* [ASM] change env variable name to remove experimental keyword (#5478)
* [ASM][IAST] Add exception for Vary: Origin (#5486)
* Send Rasp settings. Change codeowner file for snapshots. (#5490)
* [ASM] RASP: Lfi reporting (#5491)
* [ASM] Rasp: Block SSRF attacks (#5507)
* [ASM] Add stack traces to the span for RASP vulnerabilities (#5515)
* [ASM] Add RASP telemetry (#5527)

### Continuous Profiler
* [Profiler] Fix crash in Sampler at shutdown (#5483)
* [Profiler] Fix various thing in the profiler testing infrastructure (#5495)
* [Profiler] Fix bug debug info store & line of code (Code viewer) (#5496)

### Debugger
* [Dynamic Instrumentation] Fix `System.ArgumentNullException` while processing span decoration probes with empty tags (#5444)
* [Debugger][Test] skip some tests that fail in `DEBUG` mode (#5452)
* [Dynamic Instrumentation] DEBUG-2320 Support pointer and pinned local (#5464)
* [Dynamic Instrumentation] DEBUG-2321 Add local pinned and pointer for instrumentation verification (#5465)
* [Dynamic Instrumentation] DEBUG-2322 Find correct member ref based on best candidate (#5467)
* [Dynamic Instrumentation] Fix PinnedLocalTest (#5511)

### Serverless
* [Serverless] add `CI_COMMIT_TAG` to be sent downstream (#5460)

### Fixes
* Fix nullable reference bugs in Kinesis integration (#5528)

### Build / Test
* Avoid more flake in smoke tests (#5413)
* Try again to fix dd-trace build (#5429)
* Refactor test infrastructure to support chunked encoding/gzip correctly (#5446)
* Add testing of `IMultipartApiRequest` for UDS, streams and gzip (#5447)
* [CI] Minor cleanup (#5450)
* Fix dotnet_tool build (#5470)
* Fix some flaky tests in IAST and sampling (#5472)
* Ignore expected CI Visibility error (#5482)
* [CLI Tool] Updated COMPlus_EnableDiagnostics Message  (#5485)
* Simplify CI Visibility snapshot names (#5488)
* [CI] Fix flake in Git Telemetry (#5497)
* Remove direct references to Datadog.Trace from Security samples (#5500)
* Fix DSM SQS tests (#5530)

### Miscellaneous
* Calltarget `ref struct` support (#5442)
* [APM] Add git reference to application telemetry (#5459)
* Update WAF to version 1.17 (#5463)
* Don't send errors from Exceptions during requests to RCM (#5466)
* Fix RegisterIastAspects signature (#5474)
* Fix git metadata collection (#5489)
* Fix race condition when initializing metadata (#5508)
* Remove some write flags from GetModuleMetaData calls (#5517)
* Prefix dynamic assemblies with "Datadog." (#5523)
* Include inner exceptions in the telemetry logs (#5529)


[Changes since 2.50.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.50.0...v2.51.0)


## [Release 2.50.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.50.0)

## Summary

- [CI Visibility] - Improvements for MSTest: custom `TestMethod`, custom `DisplayName`, and missing tests
- [IAST] - NHibernate SQLI vulnerability detection
- [ASM] - Add support for null-returning controller actions
- [Dynamic Instrumentation] - Improvements to async method handling
- [Dynamic Instrumentation] - Improvements to symbol database

## Changes

### Tracer
* [ASM] Report external wafs headers (#5178)
* Add rate limit for the log written by `TraceRateLimiter` (#5229)
* Optimize HttpMessage.GetContentEncoding using spans (#5301)
* Use `TraceId128` in log instead of `TraceId` (#5312)
* Use vendored span in HexString (#5313)
* Add support for span links (#5354)
* [Tracing] Add `IsRemote` to `SpanContext` (#5385)
* Adding nullability to CallTarget code and null checks to the ducktyping constraints proxy (#5393)
* Add try/finally to Msmq integration (#5457)

### CI Visibility
* [CI Visibility] - New CI Visibility code coverage algorithm (#5254)
* [CI Visibility] Fix some test to avoid flakiness on retries. (#5325)
* [CI Visibility] - MSTest2 Improvements and Fixes (#5381)
* [CI Visibility] - Fix ITR Code Coverage collector attach algorithm (#5412)
* [CI Visibility] - Find .git folder when using GetFrom method as a fallback (#5425)

### ASM
* [ASM][ApiSecurity] Change Api Securitysampling algorithm (#5257)
* [ASM] never log WAF at debug level, since (#5295)
* [ASM] Dont deserialize rcm payloads until they are needed for memory optimization (#5296)
* [ASM] Fix our legacy encoder benchmarks memory leak  (#5308)
* Fix how security settings are read (#5317)
* [ASM] Ensure new sample and agent are used on each test (#5339)
* [ASM][IAST] NHibernate support (SQLI Vuln) (#5347)
* [ASM] Fix nullreference exception escalation on response body instrumentation for NET Fx (#5365)

### Continuous Profiler
* [Profiler] Use a homemade implementation of linked-list (#5284)
* [Profiler] Add FlushProfile public method (#5303)
* [Profiler] Remove flakiness for GC CPU comsumption (#5323)
* [Profiler] Add callstack provider (#5328)
* [Profiler] Avoid named pipe test flackiness (#5331)
* [Profiler] Bump to libdatadog 8 (#5348)
* Update libunwind to 1.8.1 (#5358)
* [Profiler] Fix bug in case of Agent error with .NET Framework (#5368)
* Include the libunwind double-free fix (#5397)
* [Profiler] Upgrade cppcheck to 2.12 (#5398)
* Fix profiler integration tests (#5423)
* [Profiler] Move GetAppDomain to ManagedThreadInfo (#5427)
* [Profiler] Pass memory_resource around (#5434)
* [Profiler] Fix possible crash when Agent does not answer namedpipe connection (#5437)
* [Profiler] Fix use-after-free ASAN diagnostic (#5441)

### Debugger
* [Dynamic Instrumentation] Consider 3rd party assemblies on SymDB (#5380)
* [Dynamic Instrumentation] Support legacy endpoint for diagnostics uploading (#5456)
* [Dynamic Instrumentation] Reduce allocations in probe processing (#5132)
* [Dynamic Instrumentation] Handle Out Of Range exception in SymDB (#5162)
* [Dynamic Instrumentation] Fix number of locals in async method (#5131)
* [Dynamic Instrumentation] Add NotCapturedReason for unreachable local var value in async method (#5161)
* [Dynamic Instrumentation] Normalize redaction keywords + add missing keywords (#5350)
* [Dynamic Instrumentation] Acknowledge log probe capture limits (#5364)
* [Dynamic Instrumentation] Added emitting status for probes (#5372)
* [Dynamic Instrumentation] Introduce diagnostics endpoint (#5373)
* [Dynamic Instrumentation] Temporary disable system tests (#5411)
* [Dynamic Instrumentation] Fix type of local in async method (#5414)
* [Dynamic Instrumentation] Fix probe status upload + refactor upload process (#5422)

### Exception Debugging
* [Exception Debugging] Introducing the Exception Debugging product (#5163)
* [Exception Debugging] Minor post-merge fix to Exception Debugging unwinding logic (#5327)
* [Exception Debugging] Better communicate non-captured exceptions (#5371)
* [Exception Debugging] Enhanced the reporting of non-captured exceptions (#5391)

### Serverless
* [Serverless] add serverless benchmarks (#5374)
* [Serverless] update benchmark variables (#5409)

### Fixes
* Add some more `#nullable enable` (#5332)

### Build / Test
* Fix 2.7.0 XUnit tests (#5341)
* Update CI support for release branches (#4811)
* Add explicit "clean" step to clone repo (#5309)
* Clean dangling AgentWriter instances in unit tests (#5311)
* Fix lib-injection container images (#5322)
* Increase the margin for the number of threads in RuntimeMetricsWriterTests (#5329)
* Remove automatic deploy to di (#5330)
* [Test] Running AspNetCore5IastTestsFullSampling Tests Serially (#5333)
* Disable inlining for restsharp exploration tests (#5335)
* Start pushing `latest_snapshot` images for lib-injection images (#5336)
* Filtering out Timer ExitApp span (#5337)
* [Test Package Versions Bump] Updating package versions (#5338)
* [ci] Add oci package build (#5340)
* Update CODEOWNERS file for MethodSymbolResolver.cs (#5342)
* [Build] Extend Azure Service Bus testing from versions 7.4.x - 7.17.x (#5343)
* Disable Inlining to 0 for both Cake & swashbuckle tests (#5344)
* Disabling ASM Throughput Job  (#5345)
* Running all WafLibraryRequiredTest tests serially (#5346)
* Exclude known error from smoke tests (#5349)
* Update some packages (#5353)
* Fix some warnings in the samples (#5366)
* separate DSM tests for more clarity (#5367)
* Fix smoke test issue and pin versions (#5376)
* Fix some more build warnings from the samples (#5378)
* Don't print snapshots diff unless running in CI (#5379)
* [Auto instrumentation generator] Add support for nested types (#5382)
* K8s Lib Injection: Migration (#5383)
* Try fix macos unit test crash (#5384)
* Try working around missing Docker Compose v1 in hosted runners (#5386)
* _Really_ clean up before doing anything (#5387)
* Only publish `:latest` lib-injection container images on merges to master (#5388)
* Temporarily disable profiler CppCheck (#5389)
* Split the macos build into 2 jobs and parallelise (#5390)
* Ensure we also clean hidden folders (#5399)
* Use docker mirror image in GitLab instead of dockerhub (#5401)
* Remove direct reference to Samples.AspNetCore.RazorPages from integration test project (#5402)
* Set the obfuscation querystring regex to something large to avoid flake in integration tests (#5403)
* Don't specify port for Yarp test to avoid flake (#5404)
* [Test Package Versions Bump] Updating package versions (#5405)
* Update approvals for debugger async tests (#5406)
* Update GitHub token to one that's not about to expire (#5407)
* Ignore complaints from NuGet about out of support packages (#5415)
* Add an explicit "clean docker" step (#5417)
* Don't re-build everything when building the runner in CI (#5424)
* Bump the timeout of the integration_tests_windows stage (#5426)
* Bump the macos timeout (#5430)

### Miscellaneous
* [Tracer][Logs] ILogger sample in Azure Functions (#4065)
* [DSM] - Use the same header adapter on get and put (#5361)


[Changes since 2.49.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.49.0...v2.50.0)


## [Release 2.49.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.49.0)

## Summary
* Significantly improved runtime metrics performance
* Fixes for MSTestV2 integration on .NET Framework
* Fixes for IbmMq instrumentation for DSM
* Added additional integration and improvements for IAST
* Added `DD_TAGS` to snapshot query parameter for Dynamic Instrumentation

## Changes

### Tracer
* [Tracing] Support configuring `DD_TRACE_ENABLED` remotely (#5181)
* Add support for `DD_DOGSTATSD_URL` (#5224)
* Minor (potential) fixes for `NullReferenceException` (#5230)
* Warm up the query string obfuscator regex (#5266)
* Include the trace sampling priority in the span debug log (#5274)
* [Tracer] Include propagation style in the configuration log (#5275)
* Fix errors identified from telemetry (#5279)
* Allow skipping span generation in `ProcessStart` integration (#5280)
* Don't allow adding `null` to the GRPC headers (#5286)
* Add `meta_struct` capability to the tracer (#5287)
* Handle case where `SetExceptionTags()` throws (#5291)
* Use vendored spans in tags generation (#5298)
* Optimize runtime metrics (#5304)
* [Tracing] Update instrumentation point for DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED=true (#5206)

### CI Visibility
* [CI Visibility] - Enable snapshot testing of current testing framework implementations (#5226)
* [CI Visibility] - Add a rate limit to the warning message of the OriginTagTraceProcessor (#5261)
* Disable profiling in benchmarks (#5262)
* [CI Visibility] Fix MSTestV2 integration on .NET Framework (#5269)

### ASM
* [ASM][IAST] Insecure Auth Vulnerability (#5148)
* [IAST] Added tests cases for Custom Manual and Attribute spans (#5218)
* [ASM][IAST] Support manual JSON deserialisation (System.Text.Json)  (#5223)
* [IAST] XSS vulnerability (#5231)
* [ASM] Rework encoder telemetry and logs (#5234)
* [ASM][IAST] Support manual JSON deserialisation (Newtonsoft.Json) (#5238)
* [IAST] Set redaction config values according to documentation (#5242)
* [ASM] Add processors and scanners to ruleset  (#5248)
* [ASM][IAST] Support manual JSON deserialisation (JavaScriptSerializer) #5238  (#5251)
* [ASM] Exclude NHibernate from callsite instrumentation (#5265)
* [ASM][IAST] Configure maximum IAST Ranges (#5292)
* [ASM] Deactivate benchmark for legacy encoder to help CI (#5299)
* [IAST] Vulnerability and Evidence truncation (#5302)
* [ASM] Try fix memory buildup in asm benchmarks removing destructor in Obj (#5305)
* [ASM] Add dummy agent writer for benchmarks (#5307)
* [ASM] - Fix HttpRequestValidationException Error (#5221)
* [IAST] Path in location is always the fully qualified type name (#5256)
* [IAST] Fix version parsing in Dataflow (#5263)

### Continuous Profiler
* [Profiler] Force gen2 GCs to avoid test flakyness (#5273)
* [Profiler] Exclude export error message from flaky test (#5277)

### Debugger
* [Dynamic Instrumentation] Fix not equal (ne\!=) operator in EL (#5212)
* [Dynamic Instrumentation] Remove async void in SymbolsUploader (#5155)
* [Dynamic Instrumentation] Adding ddtags to snapshot query parameter (#5210)

### Build / Test
* Enable Datadog static analysis (#5057)
* Try play asm benchmark only on  appsec changed (#5066)
* Report library configuration through telemetry (#5126)
* Removing check to allow the test to create snapshots (#5246)
* [Tracer] Adding Optional Parameter for MockTraceAgent's WaitForSpans (#5253)
* [Tracer] Fixing Missing Query String for CleanUri_HttpUrlTag Tests (#5258)
* [Tracer] Updating GrpcLegacy Sample App and Tests (#5264)
* Add analyzer to avoid implicitly capturing parameters with primary constructors (#5276)
* add a test case where message attributes are null (integration tests) (#5282)
* Skip .NET Core 2.1 tests on ARM64 (#5283)
* Set big regex timeouts for tests (#5297)
* Running All Datadog.Trace.ClrProfiler.IntegrationTests Tests Serially  (#5310)
* Fix: Skip System.Text.Json tainting tests on netcore3.0 (#5314)
* Skipping 3.0 Snapshot Check (#5316)
* [Tracer] Increasing GraphQL ObfuscationQueryStringRegexTimeout To Prevent Flakes (#5255)

### Miscellaneous
* [DSM] - Fixes for IbmMq instrumentation (#5271)
* Add db name & host to sql injected tags (#5278)

[Changes since 2.48.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.48.0...v2.49.0)


## [Release 2.48.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.48.0)

## Summary

* [Tracing] Swap default propagation styles from `tracecontext,Datadog` to `Datadog,tracecontext`
- [Tracing] Fix bug where dogstatsd tries to send to the wrong hostname
- [Tracing] Fix bug when trace-agent uses chunked responses
- [ASM/IAST] Add detection of Reflection injection vulnerability
- [Continuous Profiler] Enable exception profiling by default
- [Continuous Profiler] Fix race condition in stack unwind
- [DSM] Add DSM support for SQS

## Changes

### Tracer
* Swap default propagation styles from `tracecontext,Datadog` to `Datadog,tracecontext` (#5115)
* [Tracing] special-case the "any" pattern in sampling rules (#5142)
* Remove non-backtracking regular expressions (#5194)
* Fix scenario where dogstatsd tries to send to the wrong hostname (#5222)
* Fix product data collected for tracer flare (#5228)
* Add a `ChunkedEncodingReadStream` to use when talking to the agent (#5241)
* Update `DatadogHttpClient` to support `chunked` encoding (#5244)
* Remove usage of `ArrayPool<T>` from `ChunkedEncodingReadStream` (#5247)

### CI Visibility
* [CI Visibility] Sanitize git get-objects output (#5232)

### ASM
* [ASM] Rasp callsite instrumentation (#5186)
* [ASM] increase waf timeout on flaky unit tests (#5196)
* [IAST] Enabled hash in integration tests (#5205)
* [IAST] Add support to AspectMethodReplace with struct arguments (#5213)
* [ASM][IAST] Reflection Injection (#5219)
* [ASM] Update RASP snapshots (#5233)
* [ASM] handle array list in legacy encoder (#5239)
* [ASM] Fix null reference exception (#5243)

### Continuous Profiler
* [Profiler] Refactor to optimize samples collection (#5174)
* [Profiler] Optimize AppDomainStore (#5175)
* [Profiler] Detect Single Step Instrumentation (#5184)
* [Profiling] Reduce available symbols (#5195)
* [Profiler] On linux we may crash if we unwind a thread that was already unwinding its own callstack (#5197)
* [Profiler] Cleanup compilation warnings (#5201)
* [Profiler] Enable exception profiling by default (#5202)

### Miscellaneous
* [Test Package Versions Bump] Updating package versions (#4972)
* DSM support for SQS (#4973)
* refactor SQS send/receive instrumentation code (#5120)
* [Documentation] Add a sample that configures an ASP.NET Core app with OpenTelemetry (#5203)
* Support DOTNET_EnableDiagnostics in dd-dotnet (#5208)
* Move DSM checkpointing responsibility for Kafka from API method to integration (#5211)
* Use vendored unsafe class instead of emitting IL (#5215)
* IntegrationMapper.ConvertType simplification and Ducktype optimization (#5216)

### Build / Test
* Add testing for AWS Lambda on .NET 8 (#5236)
* Don't require additional Windows SDK for Nuke desktop notification (#5192)
* Tweak OpenTelemetry sample (#5204)
* Add IntegrationIdExtensions.cs to codeowners common files (#5217)
* Force update to latest Octokit version (#5249)
* Missing generated aspects (#5225)

[Changes since 2.47.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.47.0...v2.48.0)


## [Release 2.47.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.47.0)

## Summary

* [CI Visibility] - Add configure jenkins command in dd-trace
* [CI Visibility] - Add GAC installation feature for .NET Framework
* [ASM] - Add API Security for .NET Framework
* [ASM] - Add nosql (Mongo), stack trace leak, and xpath injection vulnerability detection
* [Profiler] - Add high/low thread count metrics
* [Dynamic Instrumentation] - Add `instanceof`, `isDefined`, parent type, static, public, and protected members to expression language
* [DSM] - Simplify DSM pathway context inheritance

## Changes

### Tracer
* Make MessagePack compatible with AOT compilation (#5092)
* Fix JSON.NET warnings for NativeAOT (#5122)
* Change AsyncManualResetEvent to use a TaskCompletionSource<bool> (#5165)
* [Tracing][ASM] http query string obfuscation should be case-insensitive (#5188)

### CI Visibility
* [CI Visibility] - Add intelligent test runner CorrelationId support (#5111)
* [CI Visibility] - Add support for event proxy v4 endpoint (gzip support) (#5114)
* [CI Visibility] - Include GAC installation feature to fix compatibility with .NET Framework (#5129)
* [CI Visibility] - Update CI specs (#5137)
* [CI Visibility] - Add configure jenkins command in dd-trace ci (#5158)
* [CI Visibility] Small optimizations (#5160)
* [CI Visibility] GAC commands in dd-trace (#5167)
* [CI Visibility] - Add netcoreapp2.x support for dd-trace GAC commands (#5173)
* [CI Visibility] - Fix DiscoveryService Agent configuration callback (#5187)
* [CI Visibility] Fix MakeRelativePathFromSourceRoot method when the path is invalid (#5189)
* Changes missing on the SCI git metadata spec (#5082)

### ASM
* [ASM] Add timeouts to the tokenizers regex (#5083)
* [ASM] Allow 0 values for iast regex timeout (#5139)
* [ASM] Fix of Context's disposing too early + new encoder's limits bug (#4884)
* [ASM] Api security for netfx (#4942)
* [ASM][IAST] MongoDB Integration (#4995)
* [ASM] Adapt ASM benchmarks (#5010)
* [ASM][IAST] New scrubbing for location data (#5047)
* [ASM] Concatenate the results of multi-WAF runs (#5055)
* [ASM] Stack trace leak vulnerability detection (#5067)
* [ASM] Xpath injection vulnerability (#5113)
* [ASM][IAST] Test IAST enum (#5116)
* [ASM] Exposes and test ephemeral address (#5121)
* [ASM] Fix flaky test (#5123)
* [ASM] Prevent null references: harden ControllerExtensions integration (#5124)
* [ASM] Fix log messaage for sys tests (#5135)
* [ASM] Add RASP configuration settings (#5136)
* [ASM][IAST] Fix Tests Location  (#5143)
* [ASM] Update waf to 1.16.0 (#5164)
* [ASM] Fix building IAST instrumented tests on macOS + increase QoL on macOS (#5169)
* [ASM] when appsec rules files is not found, dont continue init process and log confusing messages (#5171)
* [ASM][IAST] Fix weakcipher on macos (#5172)

### Continuous Profiler
* [Profiler] Refactor with a support for named pipes to communicate with Datadog Agent  (#4820)
* [Profiler] Remove unused statistics method in StackSamplerLoop (#5101)
* [Profiler] Add high/low thread count metrics (#5138)

### Debugger
* [Dynamic Instrumentation] Add probe-id tag to metric probe (#5023)
* [Dynamic Instrumentation] Send metric duration in case of evaluation error (#5059)
* [Dynamic Instrumentation] Support `isDefined` in expression language (#5021)
* [Dynamic Instrumentation] Fix EL operation names (#5022)
* [Dynamic Instrumentation] Add `instanceof` expression to EL (#5024)
* [Dynamic Instrumentation] Support parent type static public and protected members in EL (#5026)
* [Dynamic Instrumentation] Add @exception to locals (like @return) (#5051)
* [Dynamic Instrumentation] Replace @exceptions with @exception in template tests (#5140)
* [Dynamic Instrumentation] Scrub stacktrace value in probe tests snapshots (#5149)

### Build / Test
* Update OpenTelemetry snapshots for 1.7.0 (#4978)
* Update CosmosDb snapshots and csproj to ignore .NET Standard warning (#4979)
* Update PublishAotCompressed and enable LZMA compression (#5070)
* Fix `Non-serializable data ('System.Object[]') found` in tests (#5089)
* Stop rebuilding dddotnet (#5103)
* Fix instrumentation definitions source generator IDE performance (#5108)
* Test source generators for incrementality (#5109)
* [Build] Update the ubuntu Microsoft-hosted agents (#5117)
* Fix Debugger's snapshot tests (#5125)
* Avoid using dotnet run in nuke (#5127)
* [Build] Finish updating recent snapshots (#5130)
* fix unit test broket on master after merge in wrong order (#5133)
* Force execute permissions on OSX (#5145)
* Fix the issue of not having branch name when azure pipeline works with a detached HEAD (#5146)
* Output branch name to console output (#5150)
* [APM] Move common (more) files ownership on tracer folder to APM group (#5151)
* Add a fast developer loop option to the build project. (#5152)
* Create and publish arm64 version for docker image `latest_snapshot` (#5170)
* Default to .net core runtime on Linux in dd-dotnet (#5177)
* [Build] Fix dd-trace-dotnet:latest_snapshot image for system-tests (#5182)
* [APM] Move common files ownership on tracer folder to APM group (#5147)
* [tests] remove unused front-end files in sample/test web apps (jQuery, Bootstrap, fonts) (#4797)

### Miscellaneous
* simplify & fix DSM pathway context inheritance (#5074)


[Changes since 2.46.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.46.0...v2.47.0)


## [Release 2.46.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.46.0)

## Summary

* [Tracing] Add support for matching sampling rules by resource name and span tags, and for using glob patterns
* [Tracing] Add support for tracer flare for faster support resolution
* [CI Visibility] Reduce overhead of intelligent test runner, payload upload, and git unshallow
* [IAST] Add support for header injection vulnerability
* [IAST] Fix false-positive related to small-integer string cache
* [Serverless] Report exception details to Lambda extension

## Changes

### Tracer
* Add `DD_TRACE_SAMPLING_RULES_FORMAT` setting (#4984)
* [tracer] Match sampling rules by resource name and span tags (#5013)
* [Tracer] Update signature parsing in the tracer's native library to account for `ELEMENT_TYPE_PTR` byte (#5042)
* [Tracer] Send the Datadog-Entity-ID header, containing either the container-id or the cgroup inode if available (AIT-9281) (#5058)
* `NullConfigurationSource` should implement `ITelemetredConfigurationSource` (#5033)

### CI Visibility
* [CI Visibility] - Update Code Coverage percentage reporting (#5032)
* [CI Visibility] - Improve git upload logic (#5039)
* [CI Visibility] - Intelligent Test Runner: reduce overhead of the default branch (#5041)
* [CI Visibility] - Enable AutomaticDecompression on CI Visibility http clients (#5043)
* [CI Visibility] - Add support for GZip compression in Multipart payloads (#5060)
* [CI Visibility] - Fix exception when the CodeCoverage environment variable value is null (#5063)
* [CI Visibility] - TestPlatform AssemblyResolver .ctor integration (#5088)
* Ignore TestPlatform SDK assembly resolve error from CI CheckBuildLogs (#5084)

### ASM
* [ASM] IAST Header injection vulnerability detection. (#4981)
* [ASM] Dont catch CallTargetBubbleUpException native side, like on managed side (#5034)
* [IAST] SourceType refactor (#5037)
* [ASM] Add max concurrent request setting for api sec (#5048)
* [IAST] Dani/asm/small string cache bugfix (#5064)

### Dynamic Instrumentation
* [Dynamic Instrumentation] Upload symbols to SymDB (with System.Reflection.Metadata) (#4782)
* [Dynamic Instrumentation] Made the captured members size flexible due to `IndexOutOfRangeException` (#5099)

### Serverless
* Fix AWS Lambda tests (#5050)
* [SLES-1357] set exception on the aws.lambda span (#5054)

### Build / Test
* [Build] adds a notification on build end (#5008)
* Add an API to create a suspended process (#5015)
* Convert integration tests to async (#5018)
* bump default kafka lib version in sample app to solve a bug with mac (#5028)
* AutoGenerator bug fixes (#5029)
* [IAST] Folder casing fix (#5035)
* Use native debugging for procdump (#5046)
* [IAST] Removed Activator calls in SourceGenerator for performance reasons (#5052)
* Minor CI fixes (#5056)
* Fix BenchmarkTests civisibility reporting (#5069)
* Fix failing GraphQL tests on .NET Core 2.1 on alpine (#5075)
* Dani/asm/source generator refactor (#5078)
* Shorting the symbol extractor tests approval names (#5079)
* Fix trimming file (#5080)
* Disable code coverage unless forced (#5090)
* Suppress build warnings we don't care about (#5096)
* Skip debugger `SymbolExtractorTest` tests (#5095)

### Miscellaneous
* Reenable tracer flare and handle debug request (#5040)
* Fix - Change DelegateInstrumentation set continuations behaviour to copy Calltarget (#5049)
* Disable remote configuration in Serverless and CI scenarios (#5053)
* Always log rejit errors (#5061)
* Include telemetry data in tracer flare (#5062)
* Update some Github docs (#5077)

[Changes since 2.45.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.45.0...v2.46.0)


## [Release 2.45.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.45.0)

## Summary

* [Tracing] Fix trace context propagation when instrumenting a YARP reverse proxy
* [dd-dotnet tool] Detect when aspnetcore out-of-process is not initialized
* [ASM] Update waf 1.15.1 and rules 1.10.0
* [ASM] Fix incorrect encoding of route data in WAF

## Changes

### Tracer
* [Tracing] Fix trace context propagation when instrumenting a YARP reverse proxy (#5025)
* [Tracer] Update SNS integration (#4712)
* Add `PushStream` support to `IApiRequest` and support chunked-encoding (#4989)
* Add peer.hostname tag to grpc clients (#4992)
* Make custom sampling rules case-insensitive (#4999)

### ASM
* [ASM] Update waf 1.15.1 and rules 1.10.0 (#4958)
* [ASM] Fix path params not being all transformed to waf encodable values (#5011)
* [ASM] Division by zero could happen if param was 0 and apisecurity is enabled (#4975)

### Tools
* Detect when aspnetcore out-of-process is not initialized (#5012)

### Build / Test
* [ASM][IAST] Fix macOS build for IAST Instrumented tests (#4993)
* [Build] Fix Tracer Native Build on MacOS ARM64 (#4969)
* Change the echo output in Samples.Console (#4982)
* More workarounds for Rider/NuGet bug (#4986)
* Try to fix the RedirectInput test (#4998)
* Explicitly sort files before generating missing nullability csv (#5000)
* speedup CI step verify_files_without_nullability (#5003)
* Disable inlining on automapper tests (#5004)
* Remove SA1010 exclusions (#5007)
* Make sure OsX ITests don't require dd_dotnet stuff (#5009)
* little quality of life improvements on integration tests (#5016)
* Fix OSX solution filter (#5027)
* Small fixes for `MockTracerAgent` and `MockHttpParser` parsing (#4988)
* Refactor `MockTracerAgent` to allow sending custom responses for any endpoint (#4997)

### Miscellaneous
* Making RCM async (#4996)
* Add helpers for creating a sentinel file and for zipping debug logs (#4987)
* Add `TracerFlareApi` implementation for sending requests to endpoint (#4990)
* Add remote-configuration + manager support for tracer flare (#4991)
* Add nullable annotations to the datadog logging files (#4994)
* Add install signature to app-started telemetry event (#5002)
* Restore log level after tracer flare (#5017)
* Temporarily disable the tracer flare functionality (#5036)

[Changes since 2.44.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.44.0...v2.45.0)


## [Release 2.44.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.44.0)

## Summary

- [Tracer] Add support for NUnit 4.0. and Aersospike 7.0.0+
- [Tracer] Add workaround for NGEN bug in .NET runtime
- [Tracer] Add logs inject + agentless logging support for Microsoft.Extensions.Telemetry
- [ASM] Missing HSTS header and unvalidated redirect vulnerability detection
- [Dynamic Instrumentation] Added type support for PII redaction

## Changes

### Tracer
* Add global tags to dynamic config (#4901)
* Revert "Add CorProfilerInfo7::ApplyMetadata call" (#4945)
* Add logs injection + direct log shipping for Microsoft.Extensions.Telemetry (#4951)
* Add support for NUnit 4 (#4959)
* Un-skip manual instrumentation tests and fix flake (#4960)
* Stop caching exceptions in lazy (#4962)
* Add support for aerospike v7 (#4974)
* [IAST] Added missing nullable enable (#4977)

### ASM
* [ASM] Hsts header missing vulnerability (#4873)
* [IAST] Unvalidated redirect vulnerability detection (#4925)
* Add fix for null key in `SecurityCoordinator` (#4961)
* [IAST] Hardcoded Secrets location bugfix (#4965)
* [ASM] Add client IP feature to the AspNetMvc integration (#4970)

### Dynamic Instrumentation
* [Dynamic Instrumentation] Added type support for PII redaction (#4941)

### Build / Test
* Increase the dd-dotnet artifact tests timeout to 30 seconds. (#4947)
* Remove test environment variable in Samples.Console (#4948)
* [Build/Test] Add GitHub Action to track files without nullable reference types (#4954)
* [Test Package Versions Bump] Updating package versions (#4956)
* Add workaround for Rider bug (#4976)


[Changes since 2.43.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.43.0...v2.44.0)


## [Release 2.43.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.43.0)

## Summary

- [Tracing] Add support for WCF Web HTTP
- [Tracing] Add support for Npgqsl v8.0.0 and ServiceStack.Redis v8.0.0
- [Serverless] Remove up to 100ms latency in AWS Lambda when flushing traces

- [IAST] Trust boundary violation vulnerability detection


## Changes
### Tracer
* Add support for WCF Web HTTP `UriTemplate`s in WCF integration (#4903)
* [DBM] Don't inject DBM attributes into `IDbCommand` twice (#4909)
* Add support for Npgqsl 8.0.0 (#4910)
* Add support for ServiceStack.Redis 8.0.0 (#4911)
* Don't send some error logs to telemetry (#4934)
* Tiny cleanup of static `Tracer` instance usages (#4936)
* Set default batch interval in serverless scenarios (#4946)

### ASM
* [ASM] Handle anonymous types when extracting object (#4865)
* [IAST] Trust Boundary Violation vulnerability implementation (#4896)
* [ASM] Downgrade middleware log if no current span found (#4932)

### Continuous Profiler
* [Profiler] Update managed projects to net462 (#4683)
* [Profiler] Bump libdatadog to 5.0.0 (#4719)

### Dynamic Instrumentation
* [Dynamic Instrumentation] Improved snapshot pruning algorithm (#4893)

### Fixes
* Handle exception in MSMQ integration (#4931)
* Add manual+automatic instrumentation tests + fix `SetUser` bug (#4938)
* Fix recheck interval in DiscoveryService (#4907)
* Add null reference checks to SNS integration (#4917)
* Add null reference checks to elasticsearch7 integration (#4918)
* Fix ASP.NET Core DiagnosticObserver bugs (#4920)
* Add null reference checks to StackExchange.Redis integration  (#4921)
* Add null reference checks to RabbitMq integration (#4922)
* Fix bugs with DogStatsD when using named pipes or UDS (#4933)
* Wire up the DD_TRACE_BATCH_INTERVAL setting (#4940)
* [ASM] Add null check for content body before running security checks (#4950)

### Build / Test
* [Profiler] Adjust profiler tests for .NET 8 (#4908)
* [IAST] Added StringAspects.Concat() micro benchmark (#4713)
* Compile native code with C++20 (#4054)
* Reinstate version mismatch tests (#4879)
* [Test Package Versions Bump] Updating package versions (#4882)
* Remove pre-.NET8 workaround for MacOS in CI (#4888)
* [CI] Make sure all Linux images use Clang 16.0.6 (#4894)
* Attempt to add ignore various C++ warnings/errors (#4898)
* Make the agent check less verbose (#4905)
* Fix memory dumps on artifact tests (#4906)
* Fix AWS SQS test snapshots (#4913)
* Increase timeout benchmarks (#4919)
* Fix `dd_dotnet` version (#4923)
* [Tests] Ignore UDS telemetry test on windows (#4926)
* Sign `dd-dotnet.exe` that we package in MSI (#4939)
* Enable diagnostic messages in dd-dotnet artifact tests (#4944)
* Skip flaky manual instrumentation tests (#4949)


[Changes since 2.42.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.42.0...v2.43.0)


## [Release 2.42.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.42.0)

## Summary

- [Tracer] Update support for W3C interoperability (default propagation, enable 128-bits by default, improve operation names)
- [Tracer] Bundle dd-dotnet tool with installers, for easier onboarding and diagnostics
- [ASM] Support full URL request tainting, HTTP header tainting, X-Content-Type misconfiguration, and hardcoded secret detection
- [ASM] Fix potential native memory management bug
- [DSM] - Add support for IBM MQ
- [Tracer] Add support for .NET 8 GA
- [Tracer] Add support for .NET Remoting

## Changes

### Tracer
* [Tracer] add propagated (`_dd.p.*`) trace tags to the correct spans (#4332)
* Create a better `OperationName` for `Activity`/`OpenTelemetry` (#4700)
* [Tracer] Enable 128 bits by default (#4827)
* [Tracer] Updates Default W3C tracecontext Propagation AIT-8606 (#4859)
* [Tracer] .NET Remoting instrumentation (#4829)

### CI Visibility
* Add support for NUnit 3.14.0 (#4825)

### ASM
* [ASM] Api security foundation for dotnet core  (#4654)
* [ASM][IAST] Hardcoded secrets (#4666)
* [ASM] Taint full url of a request in IAST (#4789)
* [ASM] Enable IAST coverage tests NET8 (#4801)
* [ASM] unknown type to encode: log debug instead of warning (#4809)
* [ASM] Add IAST header tainting integration tests (#4814)
* [ASM] X-Content-Type header missing vulnerability (#4836)
* [ASM] Add the vale of the cookie/header name in source (#4856)
* Revert "[ASM] New marshalling system for Waf.Run calls to improve spe… (#4891)

### Continuous Profiler
* [Profiler] Refactoring to better wrap libdatadog (#4763)
* [Profiler] Add a scenario with obfuscation (#4788)
* [Profiler] Skip LinuxOnly Tests in VS Test explorer (#4803)
* [Profiler] Adjust Linux smoke test to avoid flakiness (#4842)
* [Profiler/CI] Align Cppcheck commandline linux/windows (#4854)
* [Profiler] Fix compilation warnings (#4855)
* [Profiler] Fix code hotspot feature tests (#4890)

### Debugger
* Dudik/instrumentation verification fixes (#4612)
* [Dynamic Instrumentaiton] Enriched probe status uploads with version and runtime id (#4853)

### Data Streams Monitoring
* [DSM] - Initial IBM MQ support (#4776)

### Fixes
* Stop pushing to the reliability environment (#4810)
* Tiny fix for crashing if `DD_PROFILER_ENABLED` is not set (#4849)
* Fix `NullReferenceException` in SQS integration (#4860)

### Build / Test
* Build standalone trimmed version for net7.0 (#4727)
* [Test Package Versions Bump] Updating package versions (#4806)
* Fix flaky DSM transport test (#4826)
* Properly read the installed tracer version in dd-dotnet (#4828)
* Fix `config_norm_rules.json` and `config_prefix_block_list.json` (#4830)
* [CI] Rerun waf download If it times out (#4838)
* [Test] Add propagator injection unit tests using 128-bit trace-ids (#4840)
* Try enabling crash dumps in artifact tests (#4841)
* Try to fix flake in hardcoded secrets test (#4843)
* Try to fix the pipeline monitor (#4844)
* [Test Package Versions Bump] Updating package versions (#4846)
* [CI] Delete previously generated files on Windows (#4848)
* Update xunit testing to latest (#4850)
* [Build] Update deb/rpm packaging with arm64 variant (APMON-377) (#4857)
* Re-instate the building of the system-test docker images (#4858)
* Add dd-dotnet to path on Windows (#4863)
* Handle ctrl+c in dd-dotnet (#4864)
* Add dd-dotnet to path on Linux (#4866)
* Update to .NET 8 GA release (#4869)
* Fix system-test snapshot image creation (#4871)
* Actually, really, definitely, fix the system-tests docker image (#4874)
* Run ARM64 integration tests against .NET 8 (#4875)
* Add testing for Microsoft.Data.Sqlite (#4881)
* Fix the system-test docker images once and for all (#4883)
* [ASM] Fix snapshot (#4886)
* Fix localstack not starting issue (#4895)

### Miscellaneous
* Add full null checking to MongoDB and AWS.SDK integrations (#4815)
* Update source generator to record the "instrumented assemblies" (#4832)
* Add redacted-error-log telemetry collector and sinks (#4833)
* Add redaction of telemetry error logs (#4834)
* Add de-duplication of telemetry logs (#4835)
* Stop recording individual integration telemetry (#4872)
* Record duplicate log count in log-message data (#4885)
* Improve `HostMetadata.OsVersion` value (#4819)


[Changes since 2.41.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.41.0...v2.42.0)


## [Release 2.41.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.41.0)

## Summary

- [Tracer] Improvements to the diagnostic tool
- [Tracer] Bug fixes for mixed-mode assemblies, ducktyping, and `WebRequest` handling
- [Tracer] Provisional support for .NET 8
- [Continuous Profiler] Workaround for unwinding crash
- [Dynamic Instrumentation] Added PII redaction support

## Changes

### Tracer
* Enabled invariant globalization in `dd-trace` tool (#4721)
* Include Datadog.Trace in our root-descriptors file (#4723)
* Diagnostic tool improvements (#4725)
* Use dd-dotnet in dd-trace (#4732)
* Add a better error message when inspecting worker process (#4735)
* Change the installer message in dd-dotnet (#4746)
* Add provisional support for .NET 8 (#4731)
* Add error message when IIS is not found (#4774)
* [Tracer] Elasticsearch Refactor Part 1 (AIT-8764) (#4767)
* Remove the check command on OSX (#4769)
* Update context propagation when using OTEL API is Enabled (#4783)
* Properly feed the arguments to dd-dotnet (#4775)
* 
### CI Visibility
* [CI Visibility] - BenchmarkDotNet + Datadog Profiler  (#4650)

### ASM
* Improve query string obfuscation regex  (#4504, #4764)
* [ASM] Remove debug mode for unit tests (#4728)
* [ASM]Cleanup null chars (#4733)
* [ASM] Update IAST source names (#4755)
* [ASM] Correct location error (#4780)
* [ASM] Join with char separator bug fix (#4793)

### Continuous Profiler
* [Profiler] Workaround to avoid unwinding SIGSEGV signal handler (#4753)

### Debugger
* [Dynamic Instrumentation] Fixed a crash caused by improper handling of module unload (#4779)
* [Dynamic Instrumentation] Added PII redaction support (#4786)
* [Dynamic Instrumentation] Improved probe status polling mechanism + fixed snapshot issues (#4678)

### Serverless
* [Serverless] Support tracing top-level statements for AWS Lambda (#4535)
* [serverless][SVLS-579] Add span error metadata to DD Lambda Extension request headers (#4743)
* [Serverless] [Fix PR-4535] EndInvocationAsync should be awaited. (#4752)

### Fixes
* Handle potential `NullReferenceException` in DSM (#4744)
* [Tracing] Fix WebRequest bug with existing distributed tracing headers (#4770)
* Fix bug when duck typing types with very long names (#4756)
* Avoid loader injection on <CrtImplementationDetails> (#4760)

### Build / Test
* [SLN file] move new test project into `test-applications` solution folder (#4798)
* Bump logger version (#4165)
* [TESTS] Segregate samples logs (#4698)
* [TESTS]Added BenchmarkDotNet debug helper (#4717)
* Update native build to use `/FS` option (#4718)
* Tweak `CopyNativeFilesForAppSecUnitTests` to stop it doing so much work (#4724)
* [Test Package Versions Bump] Updating package versions (#4734)
* Make `compare_throughput` stage more robust (#4739)
* Merge `throughput_appsec` into `throughput` stage (#4740)
* Fix flaky DSM test (#4741)
* [Test Package Versions Bump] Updating package versions (#4742)
* Disable profiling integration tests on non-profiler PRs (#4745)
* [Test Package Versions Bump] Updating package versions (#4758)
* [DD Tool] Port message changes to dd tool (#4762)
* Bump Timeit version to v0.1.14 (#4766)
* [Test Package Versions Bump] Updating package versions (#4773)
* Add missing dependencies to dotnet_tool job (#4777)
* Fix installer smoke tests (#4785)
* Produce "supported versions" artifacts (#4787)
* Fix MSBuild location in Nuke (#4790)
* [Test Package Versions Bump] Updating package versions (#4792)
* Update python packages and use the new image in gitlab (#4794)
* Increase timeout for query string obfuscator unit tests (#4802)
* Try to fix flake in DSM test (#4804)
* Basic smoke tests for .NET chiseled containers (#4805)
* Fix compilation errors in CodeQL job (#4768)

### Miscellaneous
* [DSM] - Lag metrics support (original PR #4574) (#4720)
* Update duck-typing documentation with best-practice recommendations (#4761)
* Add support for NLog 5.2.5 (#4772)
* Update URLs in README.md (#4778)
* Delete Telemetry V1 implementation (#4750)
* Rename TelemetryV2 to Telemetry (#4751)

[Changes since 2.40.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.40.0...v2.41.0)


## [Release 2.40.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.40.0)

## Summary

Fixes a possible application crash when using ASM. This issue was introduced in `2.38.0`. If you are using ASM with `2.38.0` or `2.39.0`, please upgrade to `2.40.0`.

## Changes

### CI Visibility
* [CI Visibility] Add support for AWS Code Pipeline (#4714)

### Continuous Profiler
* [Profiler] Add GC benchmarks for Windows (#4708)
* [Profiler] Add Profiler_Version as exported symbol to allow version check in dumps (#4710)

### ASM
* Fix possible application crash (#4726)

### Miscellaneous
* [Tool] Updating Tool checks for Windows scenarios (#4605)

### Build / Test
* Use dd-dotnet in the nuget smoke tests (#4695)
* Fix various GitHub Actions (#4716)


[Changes since 2.39.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.39.0...v2.40.0)


## [Release 2.39.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.39.0)

## Summary

- NLog logs-injection / agentless logging no longer requires configuration changes
- [CI Visibility] Remove ApplicationKey requirement from Intelligent Test Runner and add Unskippable tests support
- [ASM] - Weak-randomness vulnerability detection
- [Profiler] - The profiler takes care of some non-restartable syscalls (ex: `select`, `poll`...): restart them if the profiler interrupted them and/or avoid interrupting them whenever it's possible
- [Profiler] - Fix crash when resolving symbols from unloaded modules (ex: ASP.NET)

## Changes

### Tracer
* Make NLog configuration optional for Logs Injection/Agentless Logging (#4616)
* Introducing the Fault-Tolerant Instrumentation: A Self-Healing Mechanism for .NET Instrumentation Failures (disabled by default) (#4470)
* Add new dd-dotnet tool (#4623)
* [Tracer] Use peer.service as dddbs for DBM when available (#4619)
* [Tracer] add `dynamodb` integration (#4627)
* Adjust default log folder when running on AAS (#4656)

### CI Visibility
* [CIVisibility] Enable GZip compression on agentless mode (#4637)
* [CI Visibility] Intelligent Test Runner - Unskippable tests support (#4645)
* [CI Visibility] Remove ApplicationKey requirement from Intelligent Test Runner (#4659)
* [CI Visibility] Ensure Code Coverage percentage is always a valid number for the backend. (#4661)
* [CI Visibility] Improve unshallowing process with fallback commands (#4663)
* [CI Visibility] LifeTimeManager handling on VSTest (#4671)
* Simplify CiVisibility Telemetry check (#4679)
* [CI Visibility] Add dropped payloads Telemetry metric (#4681)
* [CI Visibility] Fix code coverage reporter (#4689)
* [CI Visibility] Capture network exceptions for telemetry (#4699)
* [CI Visibility] Telemetry metrics (#4596)

### ASM
* [ASM] Implementation of the weak randomness vulnerability detection (#4629)
* [ASM] Fix waf memory tests flakiness (#4636)
* [ASM] Add IAST throughput tests (#4640)
* [ASM] Limit number of same warning messages in waf benchmarks (#4643)
* [ASM] Filter `AspNetCore.Correlation.*` cookies vulnerabilities (#4652)
* [ASM] Taint Newtonsoft .net framework request body parameters (#4669)
* [ASM][IAST] Evidence redaction rules update (#4685)
* [ASM] Refactor the use of enums for WAF return codes (#4688)

### Continuous Profiler
* [Profiler] Add profiler timeit-based benchmarks (#4408)
* [Profiler] Wrap socket syscalls to prevent application errors (#4488)
* [Profiler] Clean up profiler C++ code (#4587)
* [Profiler] Fix flacky unit test on Alpine (#4647)
* [Profiler] POC for named pipes based IPC (#4649)
* [Profiler] Revisit the presence of the wrapping library check (#4655)
* [Profiler] Check choco exit code and retry if needed (#4665)
* [All] Run throughput jobs when they should run (#4670)
* [Profiler] Fix issue with Sentinel One Agent (#4672)
* [Profiler] Clean up profiler benchmark tests (#4673)
* [Profiler] Change Garbage Collector frame layout (#4696)
* [Profiler] Fix unit tests flackyness on Alpine (#4697)
* [Profiler] Fix Preprocessor variable (#4707)

### Debugger
* [Debugger] Run system tests for debugger scenarios (#4657)

### Fixes
* Delete unused `Distribution` metric (#4635)
* Add missing metric in agent writer (#4706)

### Miscellaneous
* [ASM] Move files in incorrect directory (#4687)
* [DSM] - Lag metrics support (#4574)
* Revert "[DSM ] - Lag metrics support (#4574)" (#4704)
* Add support for multiple enums in telemetry source generator (#4631)
* Store CIVisibility (and shared) metrics separately from APM/ASM  (#4634)
* Add artifact tests for the "run" command (#4639)
* Source generate the metric collector implementations (#4642)
* Improve the dd-dotnet.sh error message (#4664)
* Disable LZMA on Linux (#4668)
* Publish dd-dotnet symbols with the proper path (#4674)
* [Tracer][Telemetry] Add incentives to impact the telemetry config when adding an integration (#4701)
* Record TFM value in telemetry (#4702)
* Stop using public properties in built-in record methods (#4703)
* Inject serverless headers in agentless telemetry (#4705)
* [All] Add build-id to Linux binaries (#4711)

### Build / Test
* Add a dd-dotnet test for named pipes (#4646)
* Package dd-dotnet with the tracer (#4648)
* Disable dd_dotnet AOT build in OSX (#4658)
* Updates to source generators (#4660)
* Add copyright headers to source generated code (#4662)
* Add dd-dotnet to bundle nuget package (#4667)
* Add smoke tests for `dd-dotnet.sh` and `dd-dotnet.cmd` (#4675)
* Fix flake in `TracerSettingsTests` due to using environment variables (#4677)
* Try to fix GitLab build (#4684)
* Move `cppcheck` calls to separate stage (#4690)
* Rename intermediate Linux artifacts for consistency (#4692)
* Re-enable runnign the throughput tests on all PRs (#4693)
* Bump timeitsharp version (#4694)
* Try to fix GitLab flake (#4709)


[Changes since 2.38.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.38.0...v2.39.0)


## [Release 2.38.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.38.0)

## Summary

This release mainly contains:
- A _Kinesis_ integration for tracing

## Changes

### Tracer
* [Tracer] add `Kinesis` integration (#4521)
* [ASM] Filter azure assemblies in the stack of vulnerabilities (#4577)
* Lazy initialize the `AggregatedMetrics`. (#4602)
* Fix `HeaderTagsNormalizationFixEnabled` is not saved in settings (#4604)
* Fix race-condition in DuckType (#4608)
* Restrict sending APM-related metrics when running in CI app (#4618)

### CI Visibility
* [CIVisibility] Add code coverage exclusion filters (#4507)
* Allow explicitly disabling CI Visibility (#4550)
* [CI Visibility] `dd-trace ci configure` improvements (#4572)
* [CIVisibility] - Adds support for `System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute` (#4590)
* [CI Visibility] BenchmarkDotNet instrumentation refactor and fixes (#4628)

### ASM
* [ASM] New marshalling system for Waf.Run calls to improve speed and reduce allocations (#4302)
* [ASM] Waf update to 1.14.0 (#4523)
* [ASM] IAST fix for the 0 line bug (#4568)
* [ASM] Add waf benchmark with attack (#4578)
* [ASM] Iast telemetry metrics: executed instrumentation points (#4586)
* [ASM] Update IAST instrumented tests scripts for local debugging (#4595)
* [ASM] Fix IAST MVC telemetry test in Net4 (#4606)
* [ASM] Improve waf benchmarks (#4609)
* [ASM] Add request.tainted metric (#4610)
* [ASM] Update waf ruleset to 1.8 (#4633)
* Skip flaky Waf Memory Tests (#4641)

### Continuous Profiler
* [Profiler] Add missing runtime metrics (#4436)
* [Profiler] Remove unneeded debug logs (to be replaced by frames related metrics) (#4493)
* [Profiler] Add tests for Wrapper library + pthread_create for alpine (#4560)
* [Profiler] Improve integration tests output (#4569)
* [Profiler] Fix log message to have thread id in hexadecimal (#4583)
* [Profiler] Avoid returning managed thread with invalid handle (#4584)
* [Profiler] Fix vcxproj and filters (#4585)
* [Profiler] Add thread start/stop events to timeline (#4597)

### Serverless
* [Serverless][AWS] Fix serialization to use `JsonConverter` (#4559)
* Refactor `LambdaMetadata` settings creation (#4624)
* Disable telemetry metrics when running in serverless environment (#4625)

### Fixes
* Fix runtime metrics bugs + allow DD_TAGS tagging (#4580)

### Build / Test
* Change Samples.TracingWithoutLimits to use Samples.Shared (#3916)
* Swap Samples.WebRequest to use Activity (#4101)
* Assert that we don't timeout for MockTracerAgent (#4486)
* Add a retry to the macos unit tests (#4571)
* Try to remove more flake (#4581)
* Add separate log folder for throughput run (#4594)
* Crank: add logs as artifacts  (#4607)
* Fix HotChocolate tests and bump version to latest (#4614)
* Fix flake in smoke tests (#4617)
* Fix GraphQL integration tests (#4620)
* Fix flake in telemetry metric unit test (#4622)
* Fix `docker-compose stop` related flake (#4626)
* Update CODEOWNERS for serverless (#4630)

### Miscellaneous
* Initial version of the Datadog AutoInstrumentation Generator (#4422)
* [ASM] forgotten user blocking capability (#4545)
* Set the `Integration.Enabled` telemetry flag for direct log submission (#4552)
* [Test Package Versions Bump] Updating package versions (#4555)
* [Tracing] Add first-class tracing support for Azure Service Bus (#4575)
* [DSM] Add datastreams monitoring to Azure Service Bus integration (#4576)
* DuckType improvements (#4582)
* Use alpine base for lib-injection image (#4589)
* Update native log verbosity (#4591)
* [Test Package Versions Bump] Updating package versions (#4593)
* Delete unused helper (#4600)
* Add support for "missing" tag values in telemetry metrics (#4601)
* Support for Delegate instrumentation (#4613)
* Ducktyping `ValueWithType` struct support (#4621)
* Enable v2 telemetry by default (#4638)


[Changes since 2.37.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.37.0...v2.38.0)


## [Release 2.37.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.37.0)

## Summary

[DSM] - Report default time in queue and payload size

## Changes

### Tracer
* [Tracer] Fix telemetry metrics and add validator (#4543)
* [Tracer] use 2^64-1 as the modulo in the sampling formula (#4548)
* [Tracer] Make sure Process is not used concurrently (#4567)

### CI Visibility
* [CI Visibility] `ConfigureCiCommand` fixes. (#4558)
* [CI Visibility] Update CI specs (#4565)

### ASM
* [ASM] Create new appsec waf benchmark with benchmark agent (#4534)
* [ASM] Fix ASM WAF benchmarks (#4547)
* [ASM] DecompileDelegate lib bugfix (#4554)
* [ASM] bugfix - stop misreporting RC update failure (#4557)

### Continuous Profiler
* [Profiler] Add Sample value type provider (#4480)
* [Profiler] Fix race in ManagedThreadList class (#4513)
* [Profiler] Prevent symbols resolution from crashing on Windows (#4564)

### Build / Test
* Add OpenTelemetry Benchmarks (#4381)
* Test Package Versions Bump, Updating package versions (#4539)
* Bump the version of GRPC tested by default (in Windows) (#4542)
* Validate that we only use known configuration keys in telemetry (#4546)
* Fix flake in StackExchange.Redis integration tests (#4549)
* Fix warning in sample (#4551)
* Add a GH action for forcing required checks for version-bump PR to success (#4556)
* Fix version-bump PR forcer action (#4563)

### Miscellaneous
* [DSM] - Default time in queue + payload size (#4520)
* Migrate from Spectre.Console.Cli to System.CommandLine (#4395)


[Changes since 2.36.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.36.0...v2.37.0)


## [Release 2.36.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.36.0)

## Summary

This release:
- Fixes a regression with runtime metrics introduced in 2.31.0 where memory was not reported correctly
- WebRequest 4xx responses will not be reported as error if excluded by `DD_HTTP_CLIENT_ERROR_STATUSES`
- Add support for identifying PCF container IDs
- [ASM] Add support for trusted IP
- [ASM] Add shell commands integration
- [IAST] Add SSRF and LDAP injection vulnerability injection

## Changes

### Tracer
* [Agent] Do not retry response codes: 429, 413, and 408 (#4401)
* [Tracer] Consistent APM tagging for AWS requests (#4474)
* [Tracer] Logs The Setting used by dbm_propagation_mode to TracerManager (#4476)
* [Tracer] _dd.base_service now keeps the original service name when changed (#4481)
* [Tracer] Only tag Spans when DBM TraceParent is Injected (#4483)
* [Tracer] Fix WebRequest span when HTTP status code is 4XX (#4527)
* Add support for identifying Pivotal Cloud Foundry container IDs (#4536)

### CI Visibility
* [CIVisibility] Add new Intelligent Test Runner tags (#4458)

### ASM
* [ASM] Add Shell Commands collection (#4181)
* [ASM] IAST SSRF vulnerability detection (#4451)
* [ASM] Update waf to version 1.12.0 (#4482)
* [ASM] add trusted ip capablity (#4503)
* [ASM] IAST: Ldap injection vulnerability (#4506)
* [ASM] Improve the stack walker performance (#4537)
* [ASM] Filter new assemblies in the vulnerability stack (#4501)
* [ASM] Use of filters in the IAST stack walker (#4522)

### Continuous Profiler
* [Profiler] Clean up logs (#4418)
* [Profiler] Check HRESULT for generic parameter enumeration (#4443)

### Serverless
* [Serverless] Spawn mini agent in GCP & Azure Functions (#4204)

### Fixes
* Fix runtime metric generation (#4531)

### Build / Test
* Run all the system tests in CI (#4416)
* Various CI improvements (#4447)
* Fix GraphQL tests not running against package-versions API (#4455)
* [Build] Stop running master on the weekend (#4467)
* [Test Package Versions Bump] Updating package versions (#4473)
* Bump timeit version (#4478)
* Assorted speed improvements to tests (#4479)
* Fix macOS flaky unit test  (#4484)
* Bump timeitsharp version to v0.0.15 (#4485)
* Update java version to 1.17 (current LTS version) (#4496)
* Update pull_request_template to mention approval requirements (#4505)
* [Test Package Versions Bump] Updating package versions (#4510)
* CI fixes to increase reliability (#4512)
* More CI fixes to reduce flake (#4514)
* [Test Package Versions Bump] Updating package versions (#4517)
* Fix some flake and speed up some integration tests (#4519)
* Add Nuke helper for performing CPA on pipeline results (#4524)
* Add github action to run the generate package versions target (#4525)
* Split system tests as they're on the critical path (#4526)
* Fix flake in Metrics Telemetry collector tests (#4515)

### Miscellaneous
* Populate extra_services field (#4419)
* Add docs about testing automatic instrumentation (#4477)
* [Telemetry] Aggregate metrics in a separate loop (#4491)
* Don't include app-heartbeat in the first message-batch (#4497)
* Add Benchmarks public dashboard to the Readme file (#4502)
* Enable V2 Telemetry and Metrics by default in AAS (#4518)
* [Tracer] Explicit cases where configuration telemetry isn't recorded (#4464)


[Changes since 2.35.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.35.0...v2.36.0)


## [Release 2.35.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.35.0)

## Summary

This release mainly contains the last details of the preparatory work for the new service representation. It also disables the DBM and APM connection when executing a `StoredProcedure` command.

## Changes

### Tracer
* [Tracer] v1 Schema: Update AWS SNS spans (#4427)
* [Tracer] Add peer.service to RabbitMQ integration (#4341)
* [Tracer] Avoid including peer.service in kafka inbound flow (#4428)
* [Tracer] Separate inbound, outbound and client flows for msmq (#4432)
* [Tracer] Add peer.service to Service Remoting integration (#4435)
* [Tracer] Don't inject DBM data into stored procedures (#4466)
* [Tracer] Ensure the configuration of the trace context propagator is included in our telemetry (#4460)

### Miscellaneous
* [RabbitMQ] Add tracing for basic.consume, and add queue name as tag to DSM data on consume (#4398)
* Add known limitations from method_rewriter.cpp to docs (#4405)
* Update duck typing docs (#4417)
* [Test Package Versions Bump] Updating package versions (#4441)

### Build / Test
* Fix aspect definition order once and for all (#4438)
* Various test improvements (#4439)
* Fix `create_draft_release` when not specifying a specific commit (#4446)
* Update the Rel Env docker-image workflow (#4448)
* Change di backend repo to run dogfood pipelines (#4449)
* [Release] Remove the extra CI variables (#4452)
* Tweak GitLab jobs (#4453)
* Fix flaky rabbitmq datastreams test (#4457)


[Changes since 2.34.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.34.0...v2.35.0)


## [Release 2.34.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.34.0)

## Summary

- Improved WCF support for being/end async operations and distributed tracing over TCP
- Improvements to OpenTelemetry support
- Add signature to methods in Profiler

## Changes

### Tracer
* Replace the spinlock by a monitor in dogstatsd (#4328)
* Log header warnings only once (#4369)
* [Tracer] v1 Schema: Update AWS SQS spans  (#4386)
* [Tracer] v1 Schema: Update Aerospike spans (#4333)
* [Tracer] v1 Schema: Allow Peer.Service override (#4335)
* [Tracer] v1 Schema: Update AWS SQS `inbound` spans (#4434)
* [Tracer] Use ScopeContext instead of deprecated MDC for NLog 5.0.0 and higher (#4223)
* Improve NativeCallTargetDefinition marshalling (#4326)
* Fail gracefully when running with NetFx40_LegacySecurityPolicy (#4358)
* [Tracer] [Kafka] Directly use SetMetric instead of SetTag (#4370)
* [Tracer] WCF integration to read WCF message headers (#4387)
* Default to `Activity.OperationName` for `Span.OperationName` (#4409)
* Truncate mongodb query tag to 5K and Remove Generic Binary (#4421)
* Add better support for begin/end async operations in WCF (#4423)

### CI Visibility
* [CI Visibility] - Fix ITR skipped test spans (#4352)
* Add SetTag(string, double?) extension method (#4380)
* Add TelemetryMetric to SetTag and remove GetTagObject (#4389)

### ASM
* [ASM] fix error: run the waf once outside of the loop (#4338)
* [ASM] Check the path params count once list is finally built (#4365)
* [ASM] Fix appsec status log message (#4397)
* [ASM] Added 1 hour timer to vulnerability deduplication (#4411)

### Continuous Profiler
* [Profiler] Add signature to methods name (#4170)
* [Profiler] Fix bug in GC threads reporting feature (#4351)
* [Profiler] Bump libdatadog to 3.0.0 (#4356)
* [Profiler] Fix a potential bug in libunwind-datadog (#4366)
* [Profiler] Fix frame store for allocation recorder (#4382)
* [Profiler] Fix flacky test (#4383)

### Dynamic Instrumentation
* [Dynamic Instrumentation] Fixed NRE in locals name mapping   (#4348)

### Telemetry
* Refactor `TelemetryControllerV2` to be a singleton (#4354)
* Update Telemetry transport retry handling (#4371)
* Include `app-closing` in final batch (#4375)
* More telemetry fixes (#4359)
* [Telemetry] Capture metrics on fixed 10s interval and avoid drift in timer (#4388)
* Minor refactor of `BatchingSink` (#4402)

### Miscellaneous
* [BugFix] Break recursion in GetTypeInfo (#4415)
* Fix IntegrationDefinitions generics and null parameter types (#4407)
* [Test Package Versions Bump] Updating package versions (#4161)
* Bump to Serilog v3 (#4294)
* Ignore telemetry.sdk.* tags in OpenTelemetry 1.5.1 snapshots (#4330)
* Enforced order in aspects generator (#4342)
* [Diagnostic] Report process name  (#4353)
* [Test Package Versions Bump] Updating package versions (#4363)
* [Test Package Versions Bump] Updating package versions (#4400)
* [Test Package Versions Bump] Updating package versions (#4404)
* [Test Package Versions Bump] Updating package versions (#4414)
* Swap to Datadog as default propagation style (#4420)
* Revert "Swap to Datadog as default propagation style (#4420)" (#4442)
* [ASM] Waf version upgrade to 1.11.0 (#4355)
* Revert "[ASM] Waf version upgrade to 1.11.0 (#4355)" (#4430)

### Build / Test
* Remove AWS Lambda integration tests for <net6.0 (#4391)
* Add version/license/description to .deb and .rpm packages (#4377)
* Update test agent configuration (#4378)
* [Release] Fix the onboarding pipeline (#4347)
* Fix generated aspects order (#4379)
* Bump .NET SDK to 7.0.306 (#4289)
* [Release] Deploy AAS test apps twice a week to monitor memory leaks (#4322)
* [CI] A try to reducing flakiness (#4336)
* Add INTEGRATIONS system tests to Dotnet Tracer CI (#4345)
* Fix system-tests lib-injection (#4349)
* update CI parametric tests (#4350)
* Update GitLab code-sign step (#4374)
* Fix "obsolete warnings in NLog sample (#4384)
* Download cppcheck from blob storage and check the hash (#4392)
* Add AWS SNS integration automated tests, interation 1 (#4394)
* Pin aerospike server to single version (#4396)
* [Tests] Reintroduce retry on RuntimeMetricsTests with NamedPipes (#4406)
* [Builds] Update Reports comments to be updates (#4425)


[Changes since 2.33.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.33.0...v2.34.0)


## [Release 2.33.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.33.0)

## Summary

- [Serverless] AWS SDK SNS context propagation (broken in 2.32.0) has been fixed.
- [Tracer] Preparatory work for Dynamic Configuration and new service representation.
- [Tracer] Telemetry about the tracer's health can now be collected. It is disabled for now but will be rolled out slowly in the coming weeks.
- [Profiler] Report GC CPU time (disabled by default for now)

## Changes

### Tracer

#### v1 Schema
* [Tracer] v1 Schema: Update gRPC client spans (#4243)
* [Tracer] v1 Schema: Update CosmosDb spans (#4247)
* [Tracer] v1 Schema: Add peer.service tag to StackExchange and ServiceStack spans (#4261)
* [Tracer] v1 Schema: Update Couchbase spans (#4278)
* [Tracer] v1 Schema: Use existing tags for `_dd.peer.service.source` (#4306)
* [Tracer] v1 Schema: Update MSMQ spans (#4309)

#### Dynamic configuration
* [Tracer] Implement dynamic configuration (#4235)
* [Tracer] Move RCM state to the subscription manager (#4304)

#### Telemetry
* [Tracer] Send the telemetry app-started event only once (#4319)
* [Tracer] Add more telemetry metrics (#4269)
* [Tracer] Fixes for telemetry (#4291)
* [Tracer] Fix integration_name tag name (#4334)

#### Fixes
* Extract OpenTelemetry exception attributes (#4273)
* [Tracer] Fix Kafka span.kind tag regression (#4310)
* [Tracer] Fix `DuckTypeException` in earlier CosmosDb versions (#4325)

### CI Visibility
* [CIVisibility] Improving client side commands for git metadata upload (#4312)

### ASM
* [ASM] Instrument all the string builder methods (#4276)
* [ASM] Fix: avoid racing condition errors in IAST. (#4281)
* [ASM] Change the server.request.uri.raw WAF address (#4283)
* [ASM] path params: Dont run waf if no data (#4284)
* [ASM] Add support for ASM metrics (#4293)
* [ASM] Implement WAF metrics - part 1 (#4297)
* [ASM]  User events auto instrumentation in .Net Core: extended mode (#4301)
* [ASM] Insecure cookies vulnerability (#4317)

### Continuous Profiler
* [Profiler] Add CPU time for GC threads (#4256)
* [Profiler] Adjust label value to make it cleaner to display (#4280)
* [Profiler] Propagate git repository url and commit hash from tracer to profiler (#4298)
* [Profiler] Remove Profiler Github Actions CI (#4300)
* [Profiler] Enable new profiler and internal features for sanitizer jobs (#4318)
* [Profiler] Avoid loading more than 1 profiler instance (#4321)
* Fix compilation warnings (#4324)

### Debugger
* [Dynamic Instrumentation] Installing unbound probes upon module load (#4132)
* [Dynamic Instrumentation] Enable mixed instrumentation types on the same method (#4231)
* [Dynamic Instrumentation] Fixed update of probes (#4315)

### Serverless
* Fix SNS Context Propagation (#4305)

### Miscellaneous
* Use string create if net6.0 or greater (#4183)
* Allow shared call targets instrumentation with flags  (#4271)
* Revert removing IL2026 warning from customer trimming process. (#4295)
* Add CorProfilerInfo7::ApplyMetadata call (#4296)
* Handle new StyleCop errors (#4299)
* [Release] Sign and upload our packages to the agent repository for libinjection (docker/bare metal) (#4303)
* Fixup Datadog.Trace.sln file so it loads correctly on VS 2022 (#4314)

### Build / Test
* Add Windows support for -TestAllPackageVersions (#4277)
* Fix more trim warnings (#4282)
* [Tooling] Use pagination for code freeze (#4285)
* [Release] Deploy AAS test apps twice a week to monitor memory leaks (#4286)
* [Build] Remove the 3rd party pipeline (#4307)
* Bump NuGet.CommandLine from 5.9.2 to 5.11.5 in /tracer/build/_build (#4323)
* Fix the namedpipes runtime metrics tests (#4329)
* [Release] Move git tag creation back to before creating the release (#4339)


[Changes since 2.32.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.32.0...v2.33.0)


## [Release 2.32.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.32.0)

## Summary

- **Trimmed apps support**: You can now instrument trimmed apps by including the [Datadog.Trace.Trimming](https://www.nuget.org/packages/Datadog.Trace.Trimming/) nuget package to your project. This is a pre-release, do not hesitate to share feedback through Github issues.
* AWS SDK SNS is now supported by the tracer. 

## Changes

### Tracer

* Add app trimming support (Datadog.Trace.Trimming nuget package) (#4195)
* [Tracer] Add TraceClock implementation as a DateTimeOffset.UtcNow optimization (#4212)
* [Tracer] Fix TracerManager initialization when replaced (#4218)
* [Tracer] AWS SDK SNS integration (#4084)
* [Tracer] v0 Schema: Add opt-in configuration for peer.service and client service name removal (#4207)
* [Tracer] v1 Schema: Add peer.service tag to HttpClient and WebRequest spans (#4174)
* [Tracer] v1 Schema: Add peer.service tag to AdoNet spans (#4176)
* [Tracer] v1 Schema: Update operation name for WCF server integration (#4216)
* [Tracer] v1 Schema: Update operation name for gRPC server integration (#4217)
* [Tracer] v1 Schema: Update Elasticsearch spans (#4254)

### CI Visibility
* [CIVisibility] Fix repository and branch extraction in ITR and GitUpload (#4272)

### ASM
* [ASM] SqlInjection instrumented tests (EF, Sqlite, Dapper, SqlCommand, Linq2db) (#4168)
* [ASM] .NET Framework response headers (#4173)
* [ASM] static hash code for strings & ints (#4178)
* [ASM] User events auto instrumentation in .Net Core (#4184)
* [ASM] Instrumented tests for MySql, Oracle and Postgress (#4206)
* [ASM] Netcore response headers passed to the WAF (#4209)
* [ASM] Improve support of uri and response status addresses (#4211)
* [ASM] Make library invoker static (#4240)
* [ASM] Instrument all string methods (#4253)
* [ASM] Sample little fixes , sign out action hiding controller base member (#4270)
* [IAST] Update evidence redaction suite . yml and fixes accordingly (#4205)

### Continuous Profiler
* [Profiler] Fix warnings in the profiler (#4090)
* [Profiler] Avoid exceptions in code hotspots tests to reduce flacky tests  (#4197)
* [Profiler] Try to avoid a race condition in named pipe tests (#4199)
* [Profiler] Fix reading from named pipe (#4222)
* [Profiler] Introduce IThreadInfo interface (#4233)
* [Profiler] Remove dead code (#4234)
* [Profiler] Add http client to download files as demo app (#4244)
* [Profiler] Try fixing code hotspots tests (#4250)
* [Profiler] Allow lock contention provider to upscale by duration instead of by count (#4263)
* [Profiler] Add labels to send raw lock contention data (i.e., before upscaling for timeline) (#4268)

### Debugger
* [Dynamic Instrumentation] Added the ability to dynamically add tags to APM spans from method parameters or return values (#4098)

### Telemetry and Remote Configuration
* Add bulk of telemetry V2 implementation (#4188)
* Allow enabling v2 telemetry (#4198)
* Update PublicApiGenerator + minor telemetry updates (#4214)
* Delay recording of `TracerSettings` in configuration telemetry (#4221)
* Add `[GeneratePublicApi]` to `TracerSettings` (#4232)
* Update Telemetry Metrics implementation for performance (#4251)
* Add telemetry to public settings (#4259)
* Public API telemetry (#4257)
* Small telemetry updates from feedback (#4258)
* Ensure dependency collector persists across telemetry controllers (#4255)
* Move RCM initialization in the TracerManager (#4202)
* Update RCM and Telemetry poll settings (#4262)
* [RCM] In the agent TargetsVersion is a uint64 (#4208)
* Introduce per-trace settings (#4229)

### Fixes
* Fix NullReferenceException in GitMetadataTagsProvider (#4194)
* Fix "GitMetadataTagsProvider.TryExtractGitMetadata Extremely Slow" (#4248)
* Avoid making excessive & unnecessary calls to TryGetGitTagsFromSouceLink (#4249)
* Fix `<Module>` type loader injection (#4274)

### Build / Test
* Disable nullable warnings in trimming app (#4266)
* Swap Samples.GrpcLegacy to use .NET Activity (#3879)
* Swap Samples.MongoDB to use Activity (#4097)
* Disable parallelization for LargePayloadTests integration tests (#4185)
* Fix benchmark tests (#4210)
* Include CreateRootDescriptorsFile as a dependency of BuildTracerHome (#4260)
* Fix build break that was introduced when we merged the gRPC server ch… (#4239)
* Fix build: Update Trimming.xml and add missing NuGets to IntegrationGroups (#4245)

### Miscellaneous
* Refactor cache classes to avoid emitting new methods (#4192)
* Stop using Serilog.Log (#4242)
* Add the prerelease flag to the App trimming support (#4246)
* Fix nesting of Samples.AWS.SimpleNotificationService project (#4252)
* Fix explicit interface methods instrumentation (#4264)
* Fixes and tests for telemetry  (#4265)
* Fix TraceClock ctor() (#4275)



[Changes since 2.31.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.31.0...v2.32.0)


## [Release 2.31.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.31.0)

## Summary

[Serverless] Add support for instrumentation of methods in generic base classes

## Changes

### Tracer
* [Tracing] Initial work on configuration telemetry (#4033)
* [Tracing] clean up for 128-bit trace ids (#4075)
* [Tracing] Remove Span and TraceContext locks and add a cache layer (#4125)
* [Tracing] Add `nullable` and more tests for configuration (#4139)
* [Tracing] Fix potential recursion in managed loader by moving log message (#4153)
* [Tracing] Allows Adding `service` level DBM Comment on SqlServer queries. (#3989)
* [Tracing] v1 Schema: Add peer.service tag to MongoDb and Kafka spans (#4141)

### CI Visibility
* [CIVisibility] Change the code coverage EVP subdomain to `citestcov-intake` (#4150)
* [CIVisibility] Remove the pipeline url processing (#4177)

### ASM
* [ASM] Path traversal vulnerability (#4052)
* [ASM] Taint request body (#4080)
* IAST - Evidence sensitive data redaction (#4107)
* [ASM] Add source: taint request cookies (#4120)
* [ASM] Include ASM code ownership (#4121)
* [ASM] fix bug in test that meant user agent was being repeated in snapshots (#4124)
* [ASM] Add disabled flag and integration tests (#4129)
* missing custom rules capability (#4136)
* IAST - Evidence redaction Yaml suite (#4163)
* [ASM] update ruleset 1.7.0 > 1.7.1 (#4182)
* [ASM] Include path traversal method overloads in the netstandard library (#4131)

### Continuous Profiler
* [Profiler] Allow comparison for Poisson after allocation context (#4111)
* [Profiler] Bump libdatadog to 2.2.0 (#4119)
* [Profiler] Add the possible reason of SuspendThread failure (#4133)
* [Profiler] Fix profiler clang-tidy job (#4134)
* [Profiler] Allow .balloc/.pprof allocations comparison (#4145)
* [Profiler] Add log about wrapped function (#4167)
* [Profiler] Log information about secure-execution mode (#4196)

### Debugger
* [Dynamic Instrumentation] display object fields and collection items in log probe (#3947)

### Serverless
* [Serverless] Add support for instrumentation of methods in generic base classes (#4158)

### Fixes
* Add `Debug` build stage and fix warnings (#4140)

### Miscellaneous

* Add unit tests for all settings (#4115)
* Use ReadOnlySpan<byte> on ITags source code generator. (#4123)
* [Test Package Versions Bump] Updating package versions (#4128)
* `IntegrationTelemetryCollector` should only return changed integrations (#4142)
* Record enabled products in telemetry (#4143)
* Properly handle a wrong setup where `DD_DOTNET_TRACER_HOME` isn't set (#4146)
* Removed added lines and updated existent one (#4148)
* Fix ducktype over non public struct fields (#4149)
* [ASM] Merge IAST directories (#4151)
* Refactor loader injection rewrite (#4152)
* Improvements in the startup process. (#4157)
* Upgrade Mono.Cecil to 0.11.5 (#4166)
* Headers Tags improvements (#4172)
* Small updates to telemetry in preparation for V2 (#4180)
* More telemetry v2 preparation (#4187)

### Build / Test
* Add an additional scheduled run in which we explicitly enable debug mode (#4105)
* Include signed dlls in windows-tracer-home artifact (#4164)
* OSX Improvements (#4193)


[Changes since 2.30.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.30.0...v2.31.0)


## [Release 2.30.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.30.0)

## Summary

- [Tracer] Adds support for 128-bit Trace IDs
- [ASM] Adds custom rules support
- [Continuous Profiler] Enables timestamps and GC events by default for timeline view

> **Note**
> The Datadog .NET Tracer does not support application running in partial trust.

## Changes

### Tracer
* [Tracing] add support for 128-bit trace ids (#3744)
* Change the ownership of the RuntimeMetrics statsd instance (#4060)
* Make ModuleMetadata thread-safe (#4061)
* Update `MessagePack` to 1.9.11 (#4078)
* Reduce allocations in sampling mechanism tagging (#4085)
* [Tracer] Add interfaces to standardize operation name and service name calculations across schema versions (#4088)
* Add support for MySql.Data 8.0.33 (#4109)

### CI Visibility
* [CI Visibility] Rename Intelligent Test Runner tag names (#3909)
* [CI Visibility] Update ci specs and add codefresh support (#4049)
* [CI Visibility] Add support for importing code coverage from cobertura and opencover format to the test session. (#4069)

### ASM
* [ASM] Update waf and custom rules (#4053)
* [ASM] Add methods to location json (#4063)
* [ASM] Fix ip resolution: some local ips weren't seen as local (#4067)
* [ASM] Add custom rules support (#4077)
* [ASM] Add IAST instrumented tests ownership to ASM (#4087)
* [ASM] Update WAF (#4112)

### Continuous Profiler
* [Profiler] Enable timestamps and GC events by default for timeline view (#3982)
* [Profiler] Make sure the memory dump is copied when a profiler test failed (#4047)
* Fix rare and random crash on linux (#4055)
* Investigate failing test on alpine (#4073)
* [Profiler] Fix race for endpoint profiling (#4079)
* [Profiler/Windows] Release HANDLE when Managed thread dies (#4089)

### Fixes
* Remove support for partial trust environment (#4083)
* Add `EditorBrowsableState.Never` to types that should never be invoked (#4091)

### Build / Test
* [IAST] Enable deduplication tests on net7  (#3973)
* Enable crash dumps on Windows (#3975)
* [Tracer] Update samples for log collection, agentless logging, and logs trace ID correlation (#3994)
* Update Nuke build to latest (#4000)
* [Test Package Versions Bump] Updating package versions (#4013)
* [Tracer] Attribute Schema configuration: Create distinct v0 and v1 span metadata rules (#4031)
* Update dockerfile to build native code with clang-16 (#4036)
* Add checksums for release artifacts (#4041)
* Replace Datadog.Trace.OSX.sln with a solution filter (#4048)
* [Tracer] Comprehensive package version testing fixes (#4057)
* Fix CI Visibility source root in Docker based test (#4059)
* InstrumentationVerification should override the log folder only if enabled (#4066)
* Setup python3.9 for system tests (#4070)
* Add `obj` as a folder exception in the static analysis workflow. (#4071)
* Add debuglink to linux native lib to ease debugging experience (#4072)
* Add jetbrains diagnoser to Benchmark tests (#4074)
* Reduce the pressure on threadpool in the tests (#4086)
* Option to add Datadog Profiler to the BenchmarkDotNet tests (#4094)
* [Test Package Versions Bump] Updating package versions (#4096)
* Fix BenchmarkDotNet tests build warnings (#4110)
* Allow forcing code coverage in CI (#4113)
* Stop the flake (#4114)

### Miscellaneous
* RCM refactoring from event model to subscriptions (#3983)
* [Tracer] Improvement: Make the ServiceNames immutable (#4043)
* Bump spdlog version (#4044)
* Exclude more high-cardinality dependencies from telemetry (#4050)
* Update Minimum required SDK version in Readme (#4092)
* Skip injecting loader on partial trust (#4108)
* Add `trace::profiler` null check (#4116)

[Changes since 2.29.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.29.0...v2.30.0)


## [Release 2.29.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.29.0)

## Summary

Fixes the following issues:
- Fixes BadImageFormatException that occurred when trace annotations were used in conjunction with DD_TRACE_DEBUG
- Fixes possible memory leaks

## Changes

### Tracer
* [Tracer] Attribute Schema configuration: new configuration and v1 service naming (#4019)

### Continuous Profiler
* [Profiler] Compare fixed and Poisson threshold allocation sampling/upscaling (#4016)
* [Profiler/Improvement] Make `RawSample` classes move-only (#4024)
* [Profiler] Free the error structure on profile_add (#4025)
* [Profiler] Fix possible memory leak (#4029)
* [Profiler] Run Profiler Unit tests ASAN with leak detection enabled (#4034)
* [Profiler] Make sure we catch tests that crashed in CI (#4037)
* [Profiler] Fix missing CPU samples (#4046)

### Fixes
* Add more nullable annotations to settings objects (#4028)

### Build / Test
* Capture the sample output in IAST tests (#4021)
* Fix IAST tests (#4027)

### Miscellaneous
* Add WaitForDiscoveryService test helper (#4023)
* Bump DatadogTestLogger version to 0.0.38 (#4030)
* Fix the tracer solution for OSX (#4039)
* Fix BadImageFormatException that occurred when trace annotations were used in conjunction with DD_TRACE_DEBUG (#4045)


[Changes since 2.28.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.28.0...v2.29.0)


## [Release 2.28.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.28.0)

## Summary

Fixes a native memory leak when using the Continuous Profiler. This leak was introduced in `2.26.0`. If you are using the Continuous Profiler with `2.26.0` or `2.27.0`, please upgrade to `2.28.0`.

Also the tracer now supports
- HotChocolate 13
- [Data Streams Monitoring](https://docs.datadoghq.com/data_streams/) for RabbitMQ

## Changes

### Tracer
* [Tracer] Fix ActivityContext not being honored (#3796)
* [Tracer] DataStreamMonitoring - RabbitMQ support (#3917)
* [Tracer] Add `db.redis.database_index` to redis spans (#3941)
* [Tracer] Fix duplicate logging issue with DirectLogSubmission and Serilog (#3952)
* [Tracer] Reset the sampling mechanism tag during W3C tracecontext extraction (#3959)
* [Tracer] Upgrade HotChocolate to Version 13 (#3966)
* [Tracer] Fix AAS direct log submission log correlation (#3972)
* [Tracer] Make the querystring length in http.url configurable (#3980)
* [Tracer] Add nullable annotations to settings (#4009)

### CI Visibility
* [CI Visibility] Fix BenchmarkDotNet random value in job description (#3960)
* [CI Visibility] - Refactor and changes to improve debugging and to help the datadog logger to extract env-vars. (#3990)

### ASM
* [ASM] Taint StringBuilder.Append() and AppendLine() Methods (#3931)
* [ASM] Take into account if block exception is a child  (#3944)
* [ASM] Command injection vulnerability detection (#3954)
* [ASM] Remove weak hashing redundant tests (#3962)
* [IAST] Add updated source names. (#3988)

### Continuous Profiler
* [Profiler] Keep track of sampled/real exception/lock contention count per type/bucket (#3838)
* [Profiler] Avoid adding temporary runtime id names in cache (#3958)
* [Profiler] Don't read ExitTime if the process hasn't exited (#3992)
* [Profiler] Bump libdatadog version to 2.1.0 (#3993)
* [Profiler] Fix bug when thread name is set to null or "" (#4005)
* [Profiler] Fix native memory leaks (#4020)

### Debugger
* [Dynamic Instrumentation] Implemented the Span Probes (#3955)
* [Dynamic Instrumentation] Evaluation error on metric probe (#4001)

### Build / Test
* [Samples] Add sample to demonstrate instrumentation of an ASP.NET application inside a Windows container (#3798)
* [CI] Monitor retries and retry AgentMalFunctionTests on NamedPipes (#3887)
* Swap Samples.DatabaseHelper to use Samples.Shared (#3915)
* Swap Samples.LargePayload to use SampleHelpers (#3921)
* Swap DatadogThreadTest to use SampleHelpers (#3922)
* Swap DogStatsD.RaceCondition to not use Datadog.Trace (#3925)
* Limit the concurrency in xunit (#3930)
* Swap to SampleHelpers for LogsInjectionHelper (#3936)
* Fix TimeIt jobs and replace it with TimeItSharp (#3942)
* [Tracer] Fix flaky test graphql (#3945)
* Setup python 3.9 for system tests (#3948)
* Docker Base Images. Fix snapshot generation and fix draft releases (#3950)
* Convert RabbitMQ tests to snapshot tests (#3951)
* Capture full memory dumps instead of minidumps (#3963)
* Upgrade C++ toolset to v143 (#3964)
* Split `ZipMonitoringHomeLinux` task (#3967)
* Downgrade AWS dotnet7 image (#3969)
* Update the AspNetCoreTestFixture to let aspnetcore pick a port (#3971)
* Revert "Downgrade AWS dotnet7 image" (#3977)
* Temporarily remove python install from system tests (#3979)
* Disable Microsoft.Data.Sqlite tests on .NET Core 2.1 (#3981)
* Enable CI Visibility static analysis. (#3984)
* Disable code coverage except for scheduled builds on master (#3987)
* Reduce test flakiness in `ProbeTests.MethodProbeTest` (#3995)
* Increase log level in AspNetCore2 security sample app (#3997)
* Improve support of rolling log files in tests (#3998)
* Attempt to fix the DataStreams test (#4002)
* Try fix SetDotnetPath (#4003)
* Fix exploration tests typo (#4006)
* Remove the GraphQL4 websocket tests (#4010)
* Start the PipeServer synchronously (#4011)
* [Tracer] Set GraphQL7 Sample fixed packages version (#4022)
* [Tracer] Fix test: remove WebSockets tests on GraphQL version 4 (#3999)
* [Tracer] Add support for Metrics to span metadata (#3968)
* [Tracer] Adds MySql and NpgSql snapshot tests and files (#3867)

### Miscellaneous
* Replace FMT x86 and x64 builds to a single source folder (#3943)
* Windows ARM64 support (#3953)
* [Test Package Versions Bump] Updating package versions (#3957)
* Remove validation on IIS site names (#3961)
* Add a "block list" to profiled processes in native profilers (#3965)
* [Test Package Versions Bump] Updating package versions (#3978)
* Bump DatadogTestLogger version up to 0.0.37 (#3985)
* Fix the log level and remove redundant info (#3991)


[Changes since 2.27.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.27.0...v2.28.0)


## [Release 2.27.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.27.0)

## Summary

Tracer
- Support for isolated functions in Azure
- Support for GraphQL 7
- Performance improvements in the startup time. We had slowly decreased performances since 2.9.0 by adding more features, we're back to an equivalent state now.

## Changes

### Tracer
* Add a size limit to the serialization temp buffer (#3830)
* [Tracer] add support for GraphQL 7 (#3842)
* Optimize startup time (#3856)
* [Tracer] Protect against null Activity TraceId (#3870)
* [Tracing] parse propagated tags during header extraction (#3872)
* Initial support for isolated Azure Functions (v4) (#3900)
* Capture exceptions in Azure Functions (#3905)
* Exclude `App_LocalResources.` deps from telemetry (#3906)
* Performance improvements (#3907)
* Record SourceContext in direct submission logs (#3910)
* Add usr.id when propagating (#3912)
* Add recording of HTTP status code to Azure Functions HTTP triggers (#3914)
* Capture the querystring in `http.url` for client HTTP spans (#3935)

### CI Visibility
* [CI Visibility] Update CI Environment Variables spec (#3878)
* [CI Visibility] - Fixes git parser when using a SSH signature (#3940)

### ASM
* [ASM] Update rules WAF rules to 1.5.2 (#3896)
* [ASM] Correct casing for tag values in SDK (#3893)
* [ASM] Optimize http body parsing (#3853)
* [ASM] Taint String Substring and ToCharArray methods (#3854)
* [ASM] Taint string.Remove() and string.Insert() methods in IAST (#3877)
* [ASM] Taint string.Join, ToUpper and ToLower methods in IAST (#3897)
* [ASM] Instrumentate String.Trim(), String.TrimStart(), String.TrimEnd() in IAST. (#3911)
* [ASM] Instrument String.PadLeft() and String.PadRight() methods (#3918)
* [ASM] Add integration tests for global rules switch (#3886)

### Continuous Profiler
* [Profiler ] Fixes in FrameStore (#3861)
* [Profiler] Add support for Portable PDB in the profiler (#3904)

### Debugger
* [Dynamic Instrumentation] Add metric probe for dynamic instrumentation (#3727)
* [Dynamic Instrumentation] Deploy dotnet to debugger backend demo applications (#3920)

### Miscellaneous
* [Test Package Versions Bump] Updating package versions (#3759)
* Improve OSX dev experience (#3863)
* [Tracer] Implement all system-tests W3C tracecontext behaviors (#3873)
* Fix modules locking (#3892)
* Use a wrapper for synchronized access to collections (#3895)
* DSM - Span and Pathway linking (#3902)
* [Test Package Versions Bump] Updating package versions (#3929)

### Build / Test
* Swap Samples.Telemetry to use .NET Activity (#3819)
* Refine CodeOwners (#3860)
* Fix the clone-repo template for hotfixes (#3866)
* [Test] Fix flakiness in Aerospike integration test - Attempt 1 (#3890)
* Add processors benchmarks (#3898)
* Add support for attach jetbrains products in Benchmark tests (#3903)
* Remove unused reference to Datadog.Trace in HttpMessageHandler.StackOverflowException (#3923)
* Fix codeowners file around ASM folders. (#3926)
* Test Azure Functions out of process on .NET 7 (#3927)
* Fix a few issues with Samples.Shared (#3934)
* Fix native compilation warning (#3932)


[Changes since 2.26.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.26.0...v2.27.0)


## [Release 2.26.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.26.0)

## Summary

Adjustments on some previous tracer features, improve CI test skipping scenarios.
Profiler side, handle anonymous and inner named methods.
ASM side, suspicious request blocking is implemented through a consequent waf update.

## Changes

### Tracer
* Sorted and encoded tags and added new tests (#3851)

### CI Visibility
* [CI Visibility] Await git upload metadata before child command start only on test skipping scenarios. (#3827)
* [CI Visibility] - Improve manual api initialisation. (#3843)

### ASM
* [ASM] Waf block actions configurable via Remote configuration (#3794)
* [ASM] String concat propagation (#3805)
* [ASM] Waf update to 1.8.2 (#3822)
* [ASM] Update IP collection algorithm (#3831)
* [ASM] Add missing capabilities and other minor corrections (#3855)

### Continuous Profiler
* [Profiler] Add named/anonymous methods scenario (#3817)
* [Profiler] Bucketize contention events by duration (#3824)
* [Profiler] Bump libdatadog version to 2.0.0 (#3839)

### Build / Test
* Proposal for standardized storage of installable artifacts (#3762)
* Bump DatadogTestLogger version (#3833)
* [CI] Fix the flakiness of `TelemetryControllerTests`, but not the source of the issue (#3834)
* [Tracer] Container init job to run for hotfixes (#3844)

### Miscellaneous
* Enable rollforward on minor versions in global.json (#3841)


[Changes since 2.24.1](https://github.com/DataDog/dd-trace-dotnet/compare/v2.24.1...v2.25.0)

## [Release 2.24.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.24.1)

## Summary

Hotfix to fix a potential performance issues on Windows, for .NET Framework apps. 

## Changes

### Tracer
* Stop extracting SourceLink after 100 tries (#3837)

[Changes since 2.24.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.24.0...v2.24.1)


## [Release 2.24.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.24.0)

## Summary

Tracer 
- Added support for publishing init containers that will be used for library injection in kubernetes. The feature isn't yet available though at it requires changes in the datadog agent as well, that will be released next month.

Profiler
- Reduce CPU consumption on Linux 
- Improve performance of exceptions profiling

## Changes

### Tracer
* [Tracer] Fix `otel.status_code`/`otel.library.name` from .NET Activity API (#3750)
* [Tracer] add 128-bit `TraceId` struct (#3752)
* [AAS] Bail out from starting processes if API_KEY isn't present (#3775)
* [Tracer] Fix `IDatadogLogger` analyzer warnings (#3785)
* [Tracer] APM and DBM Link Injecting SQL Comment (#3784)
* [Tracer] Add support for Aerospike 6.0.0 (#3811)

### CI Visibility
* [CI Visibility] - BenchmarkDotNet framework support (#3774)
* [CI Visibility] - Extract traits recursively in NUnit (#3777)
* [CI Visibility] - Small changes to the CI Visibility processors. (#3788)
* [CI Visibility] - Lazy initialise the ITR instance (#3789)
* [CI Visibility] - Defer await for git upload task (#3825)

### ASM
* [ASM] Instrumented tests: basic setup (#3718)
* [ASM] Set span type to "vulnerability" for iast spans in console apps. (#3747)
* [ASM] Weak hashing instrumentation for net462 (#3776)
* [ASM] Add valueparts without source in vulnerability Json (#3810)
* [ASM] Weak cipher support for .net framework (#3818)

### Continuous Profiler
* [Profiler] Add allocations recorder (#3753)
* [Profiler] Reduce memory allocation of the profiler (#3764)
* [Profiler] Validate exceptions sampling (#3767)
* [Profiler] Improve .NET exception profiler (#3770)
* [Profiler] Fix CppCheck version when installing (#3772)
* [Profiler] Fix flacky tests (#3778)
* Remove all references to `DD_DOTNET_PROFILER_HOME` (#3782)
* [Profiler] Improve building thread stat file path (#3786)
* [Profiler] Improve Linux stackwalker deadlock detection (#3787)
* [Profiler] Take cores count into account for CPU profiling (#3793)
* [Profiler/CI] Remove profiler throughput tests from Github Actions (#3795)
* [Profiler] Add I/O-bound demo application (#3797)
* [Profiler] Add CI visibility on profiler AzDo jobs (#3799)
* [Profiler] Change scenario for better chances to get samples (#3801)
* [Profiler] Fix bug when retrieving number of cores (#3802)
* [Profiler] bug fixes (#3806)
* [Profiler] Increase leak size and fix exception (#3815)
* [Profiler] Add view for EndpointsCount controller (#3823)

### Debugger
* [Dynamic Instrumentation] Support duration in debugger DSL (#3765)
* [Dynamic Instrumentation] Addressed a leakage by reusing probe data indices (#3771)

### Miscellaneous
* Extract `git.commit.sha` and `git.repository_url` from SourceLink (#3652)
* Change profiler and native loader default log dir (#3790)
* Small performance improvements (#3809)

### Build / Test
* Add debugger team as CODEONWERS to missing directories (#3766)
* Generate container images for Kubernetes Admission Controller library injection (#3769)
* [Build] Add retries for build and package commands (#3780)
* Add analyzers for `IDatadogLogger` usages (#3781)
* [Build/Test] Limit upload_container_images build stage to individual CI (#3783)
* [Build/Test] Publish official dd-lib-dotnet-init images on tagged commit (#3791)
* Fix test trigger on scheduled builds (#3792)
* [Build/Test] Add system tests for the library injection images (#3803)
* Fix MongoDb integration tests and bump to latest (#3808)
* Add test and sample project for .NET Activity API (#3597)


[Changes since 2.23.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.23.0...v2.24.0)


## [Release 2.23.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.23.0)

## Summary

Tracer

- Resolve an issue introduced in 2.14.0 that could lead to the tracer not working after an upgrade of the tracer, if the server had been rebooted in between.
- Added support for Confluent.Kafka 2.x.


## Changes

### Tracer
* PRNG improvements for trace and span ids (#3651)
* Update `TagListGenerator` to use new `ForAttributeWithMetadataName` (#3662)
* Add an enum extensions source generator (#3700)
* Add helper source generators for telemetry metrics (#3701)
* Add Analyzer for checking we don't use a public API (#3704)
* Add support for Confluent.Kafka 2.x.x (#3710)
* [Tracing] Converting to and from hex strings (#3715)
* Update log message with minimum datadog-agent version required for RCM (#3729)
* Handle multiple empty `tracestate` headers in `W3CTraceContextPropagator` (#3745)

### CI Visibility
* [CI Visibility] - Fix NUnit integration (#3696)
* [CI Visibility] Adds support for NUnit TearDown attributes (#3713)
* [CI Visibility] - CI specs updates (#3719)
* [CIVisibility] - Replace the datacollector logger error calls with warnings (#3723)
* [CI Visibility] - Add retries support for IOException in the coverage collector (#3726)
* [CI Visibility] - Ensure dd-trace uses same settings as target process (#3728)
* [CI Visibility] - Simpler CIVisibility initialization for the dd-trace runner (#3736)
* [CI Visibility] - Avoid sending the global code coverage tag if we have ITR enabled (#3740)
* [CI Visibility] - Improve test source start line for CI Visibility UI (#3755)

### ASM
* [ASM] Call site instrumentation (#3453)
* [ASM] Added parent span Id to vulnerability json. (#3684)
* [ASM] SQL injection vulnerability detection (#3694)
* [ASM] bola events api (#3703)
* [ASM] Update format of vulnerabilities Json. (#3705)
* [ASM] Fix waf library loader unit tests (#3706)
* [ASM] Fix rcm integration tests flakiness (#3714)
* [ASM] Fixed typo in function parameter (#3720)
* [ASM][GIT] Fine tuned code owners (#3721)
* [ASM] Aspects code generator tests (#3741)
* [ASM] upgrade WAF to 1.7.0 and WAF rules to 1.5.0 (#3742)
* [ASM] dispose context and nullchecks  framework side (#3749)
* [ASM] Correct copy / paste error (#3760)
* [ASM] Removed unnecesary aspects file (#3711)

### Continuous Profiler
* Move profiler CI from GitHub Actions to AzDo (#2880)
* [Profiler] Measure profiled and real allocations count/size (#3698)
* [Profiler] Migrate profiler windows integration tests to AzDo (#3737)
* [Profiler] Fix integration tests not running (#3748)
* [Profiler] New scenario: Add endpoint with dots (#3756)
* [AAS/Profiler] Allow sending profiles if Tracer is deactivated (#3697)

### Debugger
* [Debugger] Limit snapshot size (#3709)
* [Dynamic Instrumentation] Perf improvements + Added probe metadata payload (#3725)
* [Dynamic Instrumentation] Display interface properties in snapshot (#3761)

### Miscellaneous
* Only Check Clsid32 Key If Tracer < 2.14 (#3549)
* Stop using the CorProfiler singleton in rejit logic (#3670)
* [Test Package Versions Bump] Updating package versions (#3691)
* Clean up codebase using clang-tidy (#3716)
* Adds the `ci crank-import` command (#3722)
* Wrap DbCommand.Connection getter with a try/catch (#3730)
* [Tracer] Check that the file in `DD_NATIVELOADER_CONFIGFILE` exists before using it (#3738)
* Record the MVID of a loaded assembly in dependency telemetry (#3746)

### Build / Test
* Add comparison of execution benchmarks posting to PR (#3672)
* Remove sync-over-async from AWS integration and fix flake (#3695)
* Add tests that we never write general ambient env vars to the logs (#3712)
* Force all tests to run as part of scheduled builds on `master` (#3724)
* Fix osx folder in GetNativeLoaderPath() (#3733)
* Add throughput tests for manual instrumentation scenarios (#3734)
* Make the log output digestable by system tests dashboard (#3739)


[Changes since 2.22.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.22.0...v2.23.0)


## [Release 2.22.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.22.0)

## Summary

Write here any high level summary you may find relevant or delete the section.

## Changes

### Tracer
* [Tracer] Add OpenTelemetry Resources as span tags (#3572)
* [Tracer] W3C Trace Context part 3: include additional vendor values in `tracestate` header (#3578)
* [Tracer] W3C Trace Context part 4: change default header settings (#3583)
* [Tracer] Fix sending multiple logs via DirectLogSubmission when using sub-loggers (#3589)
* [Tracer] Detect the actual Redis server when connecting to a cluster (#3594)
* [Tracer] Add support for Couchbase 3.4.1 (#3596)
* [Tracer] Small clean-up in `TraceContext.AddSpan()` (#3598)
* [Tracer] Allow throwing exception of type CallTargetBubbleUpException in call target integrations (#3608)
* [Tracer] Fix Remote Configuration Service Name not being sent as lower-case + added more DEBUG-level logging  (#3614)
* [Tracer] W3C Trace Context part 5: propagate `tracestate` in version-mismatch scenario (#3630)
* [Tracer] Add missing private ctors to singleton propagators (#3631)
* [Tracer] Log the version of the native tracer in the managed logs (#3643)
* [Tracer] Wait for ReJIT request call to complete before returning from ModuleLoadFinished profiler callback (#3650)
* [Tracer] Support standardized name for polling env var (#3653)
* [Tracer] Simplify `ThreadSafeRandom` API (#3656)
* [Tracer] Add a 100ms timeout on rejit request (#3664)

### CI Visibility
* [CI Visibility] Reduce integrations required for NUnit testing framework (#3577)
* [CI Visibility] Code Coverage Payload Format v2 (#3588)
* [CI Visibility] Replace XUnit sync integration for flushing with an async one. (#3591)
* [CI Visibility] Update spec for azurepipelines with required env vars for correlation (#3592)
* [CI Visibility] Port some sync thread primitives to the async version (#3609)
* [CI Visibility] Fixes multiple thread access to the global coverage List<> with a lock (#3611)
* [CI Visibility] Remove stdout output from debug level in the CoverageCollector (#3612)
* [CI Visibility] Change GetAwaiter().GetResult() with AsyncUtil.RunSync() (#3615)
* [CI Visibility] Refactor MSTest integration and add support for nuget version 3.x (#3616)
* [CI Visibility] Enable IntelligentTestRunner configuration request by default (#3647)
* [CI Visibility] Fix dd-trace arguments handling (#3676)
* [CI Visibility] Add Logs to the dd-trace command (#3683)
* [CI Visibility] Modify the .deps.json file only when the Datadog.Trace.dll is copied (#3686)
* [CI Visibility] Fix casting error on ITR skip validator (#3689)
* [CI Visibility] Small changes to improve stability (#3693)

### ASM
* [ASM] Stop throwing exception on unknown objects (#3553)
* [ASM] SQL injection vulnerability (#3595)
* [ASM] adding missing usings to dispose (#3622)
* [ASM] Fix access violation memory exception (#3666)

### Continuous Profiler
* [Profiler] Fix sanitizer jobs (#3181)
* [Profiler] Update the architecture to support heap profiling (#3445)
* [Profiler] Add MetricsRegistry (#3576)
* [Profiler] Detect and fix CPU overlaps (#3623)
* [Profiler] Add numeric labels (#3624)
* [Profiler] Fix warnings (#3634)
* [Profiler] Fix using nanoseconds instead of milliseconds (#3638)
* [Profiler] Provides an alternative stackwalk (#3642)
* [Profiler] Review log messages (#3648)
* [Profiler] Add a test scenario where short lived threads are created over and over again (#3649)
* [Profiler] Allow multiple scenarios to run sequentially in BuggyBits (#3655)
* [Profiler] Fix deadlock due to TLS initialization while unwinding (#3661)
* [Profiler] Fix text error (#3667)
* [Profiler] Check max CPU/Wall time consumption in tests (#3673)
* [Profiler] Remove internal metrics (#3678)

### Debugger
* [Dynamic Instrumentation] Added missing tags to `RcmClientTracer` (#3405)
* [Dynamic Instrumentation] Added messages to erroneous probe statuses (#3582)
* [Debugger] Run integrations tests with different debug type and optimizations (#3618)
* [Dynamic Instrumentation] Implement expression evaluator, conditions and log probes (#3688)
* [Debugger] Add default sampling rate for log probes (#3692)

### Serverless
* Add delay to serverless tests to reduce flakiness (#3619)

### Fixes
* Modernize C++ code (#3551)

### Build / Test
* [Test Package Versions Bump] Updating package versions (#3540)
* Add integration tests for OWIN apps hosted on Microsoft.Owin.Host.SystemWeb (#3558)
* Exclude some processes from being profiled in nuke targets (#3568)
* Move maximum possible tasks from `Datadog.Trace.proj` to `Nuke` (#3573)
* Increase timeout for agent malfunction flaky test. (#3574)
* Improves ExitCodeException (#3580)
* Upload crank throughput results to AzDo artifacts (#3581)
* Removes Task.Delay dependency on TaskContinuation tests (#3584)
* Minor build fixes when testing all package versions (#3590)
* [Tracer/Native loader] Run tracer and native loader native unit tests (#3593)
* Update global.json to pin to the only supported version of the SDK (#3601)
* [Test Package Versions Bump] Updating package versions (#3602)
* Add testing against multiple package versions for OpenTelemetry.Api (#3604)
* Adds debugger break environment variable (#3613)
* Allow to build native code in Debug on Linux from Nuke (#3627)
* Write throughput results as a PR comment (#3628)
* Rework build to always build for AnyCPU instead of x64/x86 (#3632)
* Bump sdk used in CI to 7.0.101 (#3633)
* [Tracer] Update span metadata rules documentation with integration names (#3635)
* Allow azure upload via a variable (#3637)
* Fix CodeQL action and GitLab build (#3645)
* Fix execution time (startup time) benchmarks and save artifacts (#3654)
* [Tests] Refactor ASM integration tests to use IClassFixture for application and agent (#3657)
* Fix path to FakeDbCommand in execution time benchmarks (#3658)
* Drop SDK in global.json down to 7.0.100 (#3659)
* Add missing dependencies to `build_runner_tool_and_standalone` stage  (#3665)
* [Tests] Enable the parametric test suite from the system-tests repo (#3675)
* Log the PID in more test paths (#3677)

### Miscellaneous
* Add instance info to the IL dump (#3660)
* [Tracer] Fix the Datadog ServiceName for spans generated by different ActivitySource's (#3663)
* RCM requests should normalize 'service' and 'env`, but not other tags (#3669)


[Changes since 2.21.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.21.0...v2.22.0)


## [Release 2.21.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.21.0)

## Summary

> **Warning** We identified [a bug in the .NET Runtime](https://github.com/dotnet/runtime/issues/77973) in .NET 5+ that can cause crashes in rare cases. You can mitigate the issue by setting `DD_INTERNAL_WORKAROUND_77973_ENABLED=1`. However, this disables tiered JIT in affected runtimes (.NET 5, .NET 6, .NET 7), which may have a significant impact on application throughput.
>
> Note that setting this flag will have no effect or impact when set for unaffected runtimes. The root issue in the .NET runtime has been resolved and will be released in versions `6.0.13` and `7.0.2` of the runtime. No fix is available for .NET 5. Other .NET versions are not affected.

- [Tracer] Update ActivityListener to handle OpenTelemetry API methods
- [Continuous Profiler] Remove 100+ ms threshold for lock contention
- [CI Visibility] Improvements to Code Coverage

## Changes

### Tracer
* [tracer] W3C Trace Context part 2: `dd` values in `tracestate` header (#3491)
* [CLI] Check if DD_TRACE_ENABLED is set And Log if set to false (#3500)
* Keep single sampled spans when stats enabled (#3536)
* Small refactor for managed logging (#3541)
* Add support for CallTarget async continuations (`OnAsyncMethodEnd` returning a Task) (#3555)
* Updated Newtonsoft.Json to 13.0.2 (#3559)
* Adds a workaround for the dotnet runtime issue 77973 (#3506, #3579)
* Add environment variable DD_TRACE_OTEL_ENABLED  (#3531)
* [Tracer] Update ActivityListener to handle OpenTelemetry API methods (#3556)

### CI Visibility
* [CI Visibility] - Code Coverage rewrite and improvements (#3494)
* [CI Visibility] - Add validation for git metadata env vars (#3560)
* [CIVisibility] - Add CIVisibility known errors to the CheckBuildLogs task (#3564)

### ASM
* asm/waf rules enabling (#3321)
* [ASM] Security refactoring : removing instrumentation gateway and simplifying (#3442)
* [ASM] Taint request parameters (#3513)
* [ASM] Taint request headers (#3563)

### Continuous Profiler
* [Profiler] Improve Linux stackwalk (#3485)
* [Profiler] Bump libdatadog version from 0.9.0 to 1.0.0 (#3538)
* [Profiler] Move GC labels from enum to text (#3545)
* [Profiler] Bump libdatadog version from 1.0.0 to 1.0.1 (#3557)
* [Profiler] Remove per type allocation (#3561)
* [Profiler] Remove 100+ ms threshold for lock contention (#3565)
* [Profiler] Remove async call for parallel buggybits (#3569)

### Debugger
* Move debugger tests to separate project (#3526)
* Fix bug which caused `DD_WRITE_INSTRUMENTATION_TO_DISK` to stop working (#3566)
* [Debugger] Temporary disable debugger tests for `x86`, .net 3.1 and .net6.0 (#3575)

### Fixes
* Refactor launchSettings.json to load the CLR Profiler from the monitoring-home directory (#3532)

### Build / Test
* Add "analyze-instrumentation" command to dd-trace diagnostic tool  (#3408)
* Test against `net462` to fix code coverage (#3520)
* Log error code when fetching kafka topic (#3527)
* Set QUIC_LTTng=0 in docker images (#3529)
* Run arm64 and Lambda integration tests on .NET 7 (#3530)
* Add some output to the runtime metrics test (#3535)
* [Build] Add missing labels to auto labeller (#3537)
* Reduce number of testing frameworks in PRs (again) (#3542)
* Fix json fluent assertion comparion (#3546)
* UDS is a variant, not a scenario in system tests (#3567)
* Temporarily ignore error in logs from CI app (#3571)

[Changes since 2.20.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.20.0...v2.21.0)


## [Release 2.20.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.20.0)

## Summary

* Adds support for .NET 7
* Allow client-side filtering of direct log submission logs using `ILogger`
* [Profiler] Include GC pauses in profiles
* [Profiler] Improve Code Hotspots to include short spans
* [CI Visibility] Adds support for Buddy CI and improves intelligent test runner
* [Dynamic Instrumentation] Enables async method probes

## Changes

### Tracer
* [Tracer] refactor trace serialization part 3: `env`, `version`, and `_dd.origin` (#3316)
* [tracer] W3C Trace Context part 1: update propagation style settings (#3446)
* Fix Single Span Sampling tags to be metrics (#3459)
* [Tracer] Use the correct `MessagePackStringCache` methods for origin and version (#3462)
* Move SpanSampling tag names to Metrics.cs (#3469)
* [Tracer] Always add `SiteName` and `SiteType` on AAS Tags and add tags in serialization (#3472)
* Add support for .NET 7 (#3482)
* Add support for filtering `ILogger` for direct log submission (#3487)
* Include "Datadog-Container-ID" as a header RCM's configuration requests (#3488)
* [Tracer] Add link to the public doc in the logs (#3493)
* [Tracer] Move AAS metadata in settings (#3517)

### CI Visibility
* [CI Visibility] - Add a new helper method to the discovery service (#3441)
* [CI Visibility] - Add buddy ci provider support. (#3443)
* [CIVisibility] - Add fallback when pack-object returns a cross device error (#3458)
* [CIVisibility] Fix CIVisibility tests (#3463)
* [CI Visibility] - Add support for ITR backend rate limiting (#3490)
* [CI Visibility] Include Intelligent Test Runner stats tags in TestSession (#3524)

### ASM
* update rules to 1.4.2 (#3418)
* [ASM] Add IAST request configuration variables (#3432)
* [ASM] Tainted map for IAST (#3450)

### Continuous Profiler
* [Profiler] Replace usage of refcounted object by shared_ptr (#2981)
* [Profiler] Improve CodeHotspots (#3501)
* [Profiler] Fix clang tidy warnings (#3435)
* [Profiler] Add garbage collection in profile (#3476)
* [Profiler] Revisit the way Sample object are used (#3478)

### Debugger
* [Dynamic Instrumentation] Improved the instrumentation of async line probes (#3426)
* [Dynamic Instrumentation] Enable async method probe (#3504)
* [Dynamic Instrumentation] Gracefully ignore byref-like types + Added TypeRef to TypeDef resolution functionality (#3505)
* [Dynamic Instrumentation] Fixed the lookup of Symbol Method of async methods (#3481)

### Build / Test
* [Test Package Versions Bump] Updating package versions (#3383)
* Use our own VMSS for more builds (#3421)
* Fix builds being broken by "quoted things" in the commit message (#3438)
* Add missing entry to VSConfig (#3440)
* Don't use the DatadogTestLogger for local dev (#3444)
* [Build] macOS multitarget build support (x86_64 and arm64) from x64 mac (#3447)
* Update vsconfig some more (#3448)
* Add a webhook stage to the pipeline (#3449)
* [Build] - Add support for universal binary in osx (#3454)
* Bump DatadogTestLogger version (#3456)
* Pin the fpm version, because latest breaks the pipeline (#3457)
* Fix Exploration tests (#3470)
* Add Instrumentation Verification to AspNetCoreMinimalApisTests (#3474)
* Disable tiered compilation in integration tests (#3479)
* Avoid monitoring the dd-trace tool as it causes a crash on start-up (#3480)
* Handle GRPC test flakiness (#3484)
* Bump DatadogTestLogger version to 0.0.31 (#3492)
* Reduce flake by always choosing a random port for `MockTracerAgent` (#3496)
* Reenable Large payload tests (#3498)
* [Tracer] Versions conflicts tests are back (#3502)
* Force English for .NET CLI in build scripts (#3508)
* Bump DatadogTestLogger to 0.0.32 (#3509)
* Create custom image for building in GitLab (#3510)
* Reduce number of frameworks tested in PRs (#3511)
* Normalize names of logs and snapshot artifacts for consistency (#3521)
* Limit the number of jobs to the number of cores when building native code on Linux (#3477)
* [Profiler] Bump cppcheck version (#3497)
* [Profiler] Fix Benchmark jobs (#3499)
* [Profiler] Fix Windows Throughput tests (#3503)
* [Profiler] Publish symbols and install procdump as postmortem debugger (#3512)
* [Profiler] Disable tiered compilation in integration tests (#3519)
* [Profiler] Export tracer native symbols in Github Actions (#3523)
* [Profiler] Disable tiered jit compilation only when Tracer is activated (#3525)
* [Profiler] Update actions to avoid noisy warnings (#3528)

### Miscellaneous
* Removed internal edges for Kafka instrumentation (#3420)
* Update to .NET 7 SDK includes (#3455)
* Use new install_script_agent7.sh in thew sample Dockerfile (#3464)
* Improvements from CppCheck static analyzer (#3483)
* Adds the Bits .NET in our readme (#3489)
* Added DI to the README (#3514)


[Changes since 2.19.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.19.0...v2.20.0)


## [Release 2.19.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.19.0)

## Summary

Tracer
* IP Collection can now be enabled by setting `DD_TRACE_CLIENT_IP_ENABLED` (#3419)
* Automatic cleanup of log files that are 31 days or older (#3346)

## Changes

### Tracer

#### Enhancements

* Allow colons in global tag values (`DD_TAGS`) (#3328)
* Delete Log Files That are 31 Days or Older (#3346)
* Make RareSampler configurable (#3360)
* Move `SpanSampler` into `AgentWriter.WriteTrace` (#3381)
* Handle client-disconnect scenario in IIS (#3395)
* [Tracer] Add stats_computation_enabled in startup logs (#3400)
* Read performance counter values directly from memory (#3403)
* [Tracer] Reintroduce opt-in ip collection (#3419)

#### Fixes
* Fix `ILogger` direct submission when formatter returns `null` (#3398)
* Fix ASP.NET and ASP.NET Core resource name template expansion (#3409)
* Update `SetTag()` source generator to avoid incorrect usage (#3415)
* Add integration tests for Azure Functions and fix warning (#3422)
* Fix Azure Functions Runtime V4 distributed tracing (#3423)

### CI Visibility
* [CI Visibility] - Add support for agent's event platform proxy. (#3322)
* Datadog Test Logger (#3355)
* [CIVisibility] - Test session visibility support (#3389)
* [CIVisibility] Fix EVP tests by turning off the DD_TRACE_DEBUG flag (#3402)
* Fix BenchmarkDotNet Exporter (#3416)
* [CIVisibility] - Add Test custom configurations support for ITR (#3417)
* [CI Visibility] - Add support for git shallow clone (#3424)
* [CI Visibility] - CheckAgentConnectionAsync rewrite to use the DiscoveryService. (#3433)

### ASM
* [ASM] IAST request management (#3384)

### Continuous Profiler
* [Profiler] Rename DD_PROFILING_CONTENTION_ENABLED to DD_PROFILING_LOCK_ENABLED (#3414)

### Debugger
* [Dynamic Instrumentation] Activating line probe on async methods + Fixed snapshot serialization issues (#3406)

### Build / Test
* Fix OSX managed loader issue. (#3386)
* Change list of expected modified files for release (#3387)
* Fix Pipeline when Pull Request variables are not available (#3388)
* Add missing dependencies to `coverage` and `traces_pipeline` stages (#3390)
* Apply new PackageReference Conditional to fix IIS tests (#3394)
* Enable Test DataLogger (#3397)
* Small fixes for DatadogTestLogger (#3399)
* Bump version of DatadogTestLogger to remove direct reference to Datadog.Trace (#3401)
* Ensure test parameters are constant across test runs (#3404)
* Fix the env and service names for AppSec throughput tests and benchmarks (#3413)
* Add special ExitCodeException to test suites (#3425)
* Bump DatadogLogger version to 0.0.28 (#3427)
* [Build] Fix exported symbols issue with CMake builds (#3428)


[Changes since 2.18.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.18.0...v2.19.0)


## [Release 2.18.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.18.0)

## Summary

Profiler
* AAS support
* Allocation and Contention profilers are available! Enable them with `DD_PROFILING_ALLOCATION_ENABLED=1` and `DD_PROFILING_CONTENTION_ENABLED=1` respectively

## Changes

### Tracer
* Add `SpanRateLimiter` for Single Span Ingestion (#3282)
* Add `SpanSamplingRule` for Single Span Ingestion (#3283)
* Implement the SpanSampler into the Tracer (#3286)
* [Test Package Versions Bump] Updating package versions (#3300)
* [Tracer] refactor trace serialization part 2: `_sampling_priority_v1` (#3315)
* [CI Visibility] - Add a final try to load the pdb from a module. (#3350)
* Make cgroup `ContainerMetadata` parsing more lenient (#3354)
* [Test Package Versions Bump] Updating package versions (#3366)
* [Test Package Versions Bump] Updating package versions (#3376)
* Adding Tags Data to Span Started Logs (#3377)

### CI Visibility
* [CI Visibility] - Add Intelligent Test Runner support in MSTest testing framework (#3341)

### ASM
* [IAST] Implementation of the basic classes that will be used in IAST and the insecure hashing vulnerability detection (#3225)
* [ASM] Remove warnings and fix flake (#3338)
* [IAST] Weak cipher algorithms detection (#3353)
* [ASM] Deduplication of vulnerabilities (#3371)
* [ASM] Stop collecting IP if appsec is disabled, only collect ip for appsec (#3379)

### Continuous Profiler
* [Profiler] Add support for named pipes (#3257)
* [Profiler] Sample allocation and contention profilers (#3268)
* [Profiler] Send only activated profilers values in profile (#3337)
* [Profiler] Ensure all sample have high precision timestamps (#3349)
* [Profiler] Save and restore the value of errno in the signal handler (#3370)
* [Profiler] Samples supports timestamps as label (#3372)
* [Profiler] Disable namedpipe flacky test on x86 until fix is found (#3380)
* [Profiler] Customize wall time and CPU sampling constants (#3382)

### Serverless
* Update Samples.AWS.Lambda and tests (#3359)
* Add support for nested and generic arguments to Lambda handlers (#3367)
* Run serverless tests on .NET 5.0 and .NET 6.0 (#3369)
* Add better fallback for Lambda service name (#3378)

### Fixes
* Fixes Apple Silicon Build (#3358)
* Fix fairly subtle bug with remote config feature ASM_DD (#3344)
* Minor remote config fixes (#3347)
* Close the logger on exit (#3374)

### Build / Test
* Fix all build warnings and errors (#3324)
* [asm] Fix flake asm remote rules tests (#3334)
* Fix dotnet install in build (#3348)
* Exclude xml files from `dd-trace` tool and Datadog.Trace.Bundle (#3363)
* Fix broken package version bump + add GitHub action (#3364)
* Increase allowed variation in `SpanSamplerTests` to fix flake (#3368)

### Miscellaneous
* Revisit CMake projects architecture (#3329)
* Native loader build (#3357)


[Changes since 2.17.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.17.0...v2.18.0)


## [Release 2.17.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.17.0)

## Summary

- Fixes an error in instrumentation when certain code patterns are found in instrumented classes (first identified in `EFCache`)
- Fixes ASM one-click activation

## Changes

### Tracer
* Enable agent-based telemetry by default (#2800)
* [Tracer] refactor trace serialization part 1 (#3135)
* Add fix for method rewriter in non-void method with branch to last return (#3331)
* Add Data Streams Monitoring support when using manual instrumentation with Kafka (#3319)

### CI Visibility
* [CIVisibility] Public Api fixes (#3330)

### ASM
* Simplify blocking by updating the test rules (#3318)
* Use a different log file for tests using the log entry watcher (#3320)
* Correct index used by activation capability (#3333)

### Continuous Profiler
* [Profiler] Bump libdatadog version to 0.9.0 (#3313)

### Miscellaneous
* [Tracer] Use `Datadog.Trace.Bundle` in `NugetDeployment` samples (#3273)

### Build / Test
* Fix documentation URL (#3327)

[Changes since 2.16.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.16.0...v2.17.0)


## [Release 2.16.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.16.0)

## Summary

* [ASM] IP Blocking by remote config
* [Tracer] Making `Datadog.Trace.Bundle` Nuget package Generally Available. This package contains the full Datadog .NET APM suite for Tracing (automatic, and custom), Continuous Profiler and Application Security Monitoring (ASM). 
* [Tracer] Add support for instrumenting interface methods, allowing for instance for more support of some RabbitMq libraries.


## Changes

### Tracer
* Data Stream Monitoring .NET implementation
    * Initial data streams monitoring propagation (#3174)
    * Add Kafka data streams monitoring implementation (#3192)
    * Added support for `IDiscoveryService` to `DataStreamsManager` (#3220)
    * Add checkpointing and aggregation for data streams monitoring (#3191)
    * Fix edge case in `DataStreamsWriter` (#3304)
    * Disable DSM if `DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED=0` (#3311)
    * GZip the data streams monitoring data sent to the agent (#3249)
    * Make sure we remove the DSM propagation header from SQS attributes (#3312)
* Send Telemetry heartbeat independently of flushed data (#3239)
* Remove unneeded logs (#3265)
* Try loading OpenSSL eagerly on `netcoreapp2.0`-`net5.0` (#3284)
* [Tracer] Add support for instrumenting interface methods (#3256)
* [Tracer] Rename `Datadog.Monitoring.Distrib` into `Datadog.Trace.Bundle` (#3263)

### CI Visibility
* [CI Visibility] - Test API and Test Suite Visibility (#3276)
* [CI Visibility] - Initialize tracer earlier (#3295)

### ASM
* [ASM] ASM_DATA  - IP blocking data (#3235)
* [ASM] IP Blocking for .NET Framework (#3207)
* [ASM] upgrade waf and rules (#3274)
* [ASM] remote config update (#3120)
* [ASM] Fix asm RCM tests flakiness (#3266)
* [ASM][RCM] Reduce Flake > fix integration tests log watching (#3287)
* [ASM] Optimize blocking (#3306)

### Continuous Profiler
* [Profiler] Fix throughput tests on Linux (#3246)
* [Profiler] Prevent profiler from getting stuck on lost signal (#3255)
* [Profiler] Fix deadlock on alpine on thread creation (#3288)
* [Profiler] Change message when exporting profile (#3290)
* [Profiler] Change lock-duration to lock-time (#3298)
* [Profiler] Initialize signal handler only once (#3308)
* [Profiler] Log INVOKE and CALL warning message only once (#3309)
* [Profiler] Change log level from error to info in StackSamplerLoopManager (#3314)

### Debugger
* [Debugger] Adjust debugger snapshot json structure to new schema (#3297)
* [Debugger] Skip `DebuggerSnapshotCreatorTests.Limits_LargeDictionary` as it's flaky (#3310)

### Serverless
* Serverless build tidy up (#3110)
* [Serverless] Support base-class AWS Lambda methods (#3275)

### Miscellaneous
* Remove useless logs (#3230)
* Update coreclr files to fix errors on Alpine (#3260)
* Fix endian bug (#3270)

### Build / Test
* Throughput for blocking (#3277)
* Re-enable collecting crash dumps on Linux (#3227)
* Add ability to capture hang dumps on Linux (#3229)
* Update the list of expected file changes when doing a version bump (#3259)
* Samples.Probes must always be built (#3262)
* Bump timeouts in installer smoke tests (#3267)
* [CI] Fix permissions issue when uploading crash dumps on Linux in some cases (#3269)
* Exitcode should be zero (#3271)
* Give proper warning if the `createdump` tool crashes. (#3272)
* Add Apple Silicon support on tracer builds (#3279)
* Improve gitlab deployment (#3285)
* Bump some timeouts to reduce test flake (#3289)
* Fix test version package bump (#3291)
* Skip flakey test ProbesTests.TransparentCodeCtorInstrumentationTest (#3296)
* Add a workaround for missing profiling support in PyPI version of dd-apm-test-agent (#3299)
* Remove workaround for missing profiler support in PyPI version of dd-apm-test-agent (#3303)
* Run IP blocking tests with default rules (#3307)
* Fix flaky `DataStreamsWriter` test (#3317)


[Changes since 2.15.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.15.0...v2.16.0)


## [Release 2.15.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.15.0)

## Summary

- Added HotChocolate GraphQL integration (v11.x.x+)
- Added instrumentation for `Process.Start`
- Added endpoint profiling for continuous profiler

## Changes

### Tracer
* [Tracer] Tracking sampling mechanism with `_dd.p.dm` tag (#2982)
* Obfuscate ids with commas and obfuscate WCF requests (#3026)
* [Tracer] Stats computation: Drop P0 traces (#3048)
* [Tracer] Add telemetry for the stats computation feature (#3053)
* [Tracer] When stats computation is enabled, normalize and obfuscate traces (#3054)
* Fix build when using C#11 (#3102)
* Add `unchecked` to FNV hash calculations (#3109)
* Add `VarEncodingHelper` for use in Data Streams Monitoring (#3129)
* Add a thread-safe `Random` implementation (for Data streams monitoring) (#3130)
* Add hash helpers for data streams monitoring (#3131)
* Compile Regex in `CustomSamplingRule` (#3132)
* [Tracer] Add DiscoveryService to Tracer initialization and update StatsAggregator to use Agent feature `client_drop_p0s` (#3152)
* Refactor `DiscoveryService` to handle changes to settings and agent upgrade (#3154)
* Remove redundant string allocations (#3169)
* Add debug log in `Api` when sending to agent fails (#3180)
* Update DDSketch parameters for DSM to match backend (#3216)
* Add support for specifying a `Content-Encoding` in `IApiRequest` (#3238)
* Add `NullDiscoveryService` for CI visibility and testing (#3253)
* Add instrumentation for `Process.Start` (#3146)
* HotChocolate GraphQL integration (#3004)
* Fix ServiceStack SendReceive instrumentation (#3084)
* Add Kafka consumer group tag (#3099)

### CI Visibility
* [CIVisibility] Adds support to disable the tracer profiler + Lazy loading the tracer settings (#3108)
* [CIVisibility] - Counters based Code Coverage (#3134)
* [CIVisibility] - Fix InternalFlushEventsAsync task creation (#3148)
* [CIVisibility] Include ci-app-libraries-dotnet team as a CODEOWNER of PDB folder. (#3151)
* [CIVisibility] - Coverage fixes and improvements (#3170)
* [CIVisibility] - Correctly extract commit message from AppVeyor (#3183)
* [CIVisibility] - Fetch committer and author in buildkite and bitrise (#3187)
* [CIVisibility] - Move the CI Visibility check to native (#3196)
* [CIVisibility] Intelligent test runner (#3075)

### ASM
* [ASM] Blocking a request on .NET Core (#3066)
* [ASM] 1-Click activation (#3081)
* [ASM] Waf and ruleset update (#3087)
* [ASM] Increase waf timeout for all integration tests (#3124)
* [ASM] Features rename (#3223)
* [ASM] Waf memory leak fix (#3237)
* [ASM] Prevent waf from breaking app if non compatible native library file is loaded by tracer (#3251)
* Implement rcte2 - Configuration apply status (#3175)
* Remote Configuration Tracer Extension 3 - Remote Configuration Client capabilities (#3186)

### Continuous Profiler
* [Profiler] Add continuous profiler checks to dd-trace (#3167)
* [Profiler] Implement endpoint profiling (#3015)
* [Profiler] Use libdatadog as buffer (#2990)
* [Profiler] Add new profile tags (#3093)
* [Profiler] Update libdatadog to 0.8.0 (#3111)
* [Profiler] Measure exceptions and allocations profilers (#3112)
* [Profiler] Add contention profiler based on Clr Events (#3115)
* [Profiler] Activate the CPU profiling by default (#3121)
* [Profiler] Return string view for symbol resolution (#3127)
* [Profiler] Disable tracer for profiler timeit usage (#3128)
* [Profiler] Update SamplesCollectorTest (#3145)
* [Profiler] Add exceptions in BuggyBits demo app (#3150)
* [Profiler] Performance improvements for samples management (#3153)
* [Profiler] Measure contention profiler impact (#3172)
* [Profiler] Ensure that all API HRESULT are checked (#3179)
* [Profiler] Make exceptions profiler more real for BuggyBits (#3188)

### Debugger
* [Debugger] Fixed serialization issues in producing snapshots (#3064)
* Support Remote Configuration Management (#3077)
* [Debugger] Add support for probes in async methods (#3079)
* Support UDS and Named Pipes for Live Debugger (#3125)
* [Debugger] Stabilized the instrumentation of Method Probes and Line Probes (#3164)
* Remove ImmutableDebuggerSettings (#3166)
* [Debugger] Fix non-deterministic failure in UDS Integration Test (#3168)
* [Debugger] Fix Debugger Method Probe Async Tests (#3199)
* Use `LifeTimeManager` for LiveDebugger shutdown events (#3200)
* Run Remote Configuration system tests (#3212)
* [Debugger] Resolving Live Debugger's code coverage degradation (#3245)
* Disable async probe (#3248)

### Fixes
* [Tracer] Exit early during automatic instrumentation when the application is .NET Core 1.x (#3114)
* [Tracer] Refactor stats computation into StatsAggregator (#3133)

### Miscellaneous
* [Tracer] Ignore more assemblies and don't resend them all at each run (#2966)
* [Test Package Versions Bump] Updating package versions (#3008)
* [CI] Add doc to run smoke tests locally (#3088)
* Small refactoring to remove duplicate code around `SocketException` detection (#3123)
* Refactor transport strategies code (#3126)
* Refactor the exclude list for assembly instrumentation  (#3137)
* Remove redundant string allocations for ApiResponses (#3144)
* Set the version of the native loader (#3147)
* Fix warnings (#3155)
* Rename RuleBasedSampler to TraceSampler (#3156)
* Add a simple pooling mechanism for DDSketches (#3177)
* Avoid multiple configuration source enumeration (#3178)
* Update repo readme about security issues (#3185)
* Fix ICorProfilerInfo11 framework support message (#3194)
* Low hanging fruit startup optimization (#3195)
* Don't overwrite the origin (#3221)

### Build / Test
* [tests] clean up test project references (#3198)
* Remove obsolete snapshot URI (#3165)
* Fix order of spans in WcfTests (#3162)
* [Tracer] Add tests for stats computation feature (#3047)
* [Snapshots] Print a small diff at the end of the tests (#3098)
* Make sure we throw if uploading artifacts to S3 fails in gitlab (#3116)
* Automatically trigger AAS deploy with code freeze (#3118)
* Update GitLab to use newer build image (#3136)
* Skip the Razor Pages diagnostic listener tests on Linux .NET Core 2.1 (#3140)
* Enable tool integration tests on Windows (#3149)
* Exclude named pipe timeout from build log check (#3157)
* Ensure any test artifact has the job attempt number as part of the name. (#3173)
* Test `RemoteConfigurationApi` instead of using file provider (#3182)
* Add support for Microsoft.Data.SqlClient `5.x.x` (#3184)
* Schedule CI to run 10 times on a Saturday morning (#3202)
* [tests] show only diff in test failures when comparing long multi-line strings (#3205)
* Fix flakey test Http_Headers_Contain_ContainerId  (#3206)
* Fix WindowsContainer tracing sample (#3208)
* Added integration tests to verify that the tracer still sends traces if the agent doesn't work properly. (#3211)
* Small testing improvements (#3222)
* [Testing] Improve span metadata rules (#3226)
* Add manual repo clone to `unit_tests_macos` (#3232)
* Bump timeouts in `DiscoveryServiceTests` to reduce flake (#3233)
* Ensure we always run exploration tests on master (#3243)
* Add remote config label (#3244)
* [Snapshots] Diff on base branch (#3250)


[Changes since 2.14.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.14.0...v2.15.0)


## [Release 2.14.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.14.0)

## Summary

- GA release of the continuous profiler, adds support for NuGet-based deployment
- Addresses a double-billing issue in AAS

## Changes

### Tracer
* Allow null filenames in MultipartItem when using HttpClientRequest (#3024)
* Fix possible zero in traceid or spanid (#3033)
* [Tracer] Send AAS and Sampling Prio on all trace chunks (#3040)
* Add .NET Framework 4.8.1 to `FrameworkDescription` (#3100)

### CI Visibility
* [CI Visibility] Disable non testhost proc instrumentation on dotnet command (#3018)
* [CI Visibility] Refactor CI Visibility payloads with the EvpPayload abstraction (#3032)
* [CI Visibility] Stop using OS separator in relative paths (#3034)
* [Ci Visibility] - Update logs format to the new spec (#3037)
* [CI Visibility] Fix Coverage version type (#3039)
* [CI Visibility] Upload Git tree metadata (#3045)
* [CIVisibility] Change build file and CODEOWNERS (#3046)
* [CIVisibility] Add support to bypass some ci environment variables (#3082)
* [CIVisibility] Fix CIAgentlessWriter flush algorithm (#3096)

### Continuous Profiler
* [Profiler] Use uws_backtrace for stackwalking on Linux (#2867)
* [Profiler] Implement ICorProfilerCallback::EventPipeEventDelivered to receive CLR events synchronously (#2998)
* [Profiler] Do not build the profiler native code in parallel for ASAN (#3021)
* [Profiler] Check profiler log files for smoke tests (#3038)
* Revert "[Profiler] Use uws_backtrace for stackwalking on Linux" (#3042)
* [Profiler] Update libunwind to 1.6.2 (#3043)
* [Profiler] Disable profiler when running in a container with <1 CPU (#3050)
* [Profiler] Try to use default UDS path on Linux (#3057)
* [Profiler] Fix generic types name for exception and allocation profilers (#3058)
* [Profiler] Remove DD_DOTNET_PROFILER_HOME (#3073)
* [Profiler] Improve error reported by libddprof (#3089)
* [Profiler] Fix Profiler integration tests in VS (#3090)
* [Profiler] Fix Sanitizer jobs (#3091)
* [Profiler] Remove beta version for the profiler (#3092)
* [Profiler] Change profiler library output directory on linux (#3094)

### Debugger
* [Debugger] Fix issue where line probes with backslashes did not work (#3028)
* Disable non-deterministic tests (#3030)
* [Debugger] Leverage AdaptiveSampler for probes (#3078)

### Build / Test
* Rename native projects in preparation for package layout refactor (#3016)
* [CI] Allow throughput to run on benchmark branches (#3031)
* Always use native loader for tests (part of package layout update) (#3036)
* [AppSec] Enable AppSec in installer smoke tests (#3055)
* Update package layouts & ship continuous profiler in dd-trace/NuGet (#3060)
* Enable debug logs for smoke test (#3062)
* Run MSI smoke tests against x86 runtime with the x64 installer (#3065)
* Add a very simplistic snapshots checker (#3068)
* Add a few smoke tests for the `dd-trace` NuGet package (#3070)
* Small build tidy up - improve log file checking (#3071)
* Delete `UpdateMsiContents` Target and `SyncMsiContent` (#3074)
* More build tidying up (#3076)
* Fix build error in OSX when using sdk version 6.0.400 (#3083)
* [Snapshots] Add a note on version mismatch tests (#3086)
* Centralise handling of HTTP requests in `MockTracerAgent` (#3095)
* Increase smoke tests timeout (#3101)
* Force fpm gem install to not update dependencies (#3104)
* Use default monitoringHome path if env var is empty (#3105)

### Miscellaneous
* Remove x-datadog headers from SQS message attributes (#3044)
* Fix OSX native loader build issue (#3052)
* Fix race condition during profilers initialization (#3056)
* Add note on 'round-tripping' to Instrumentation Verification doc (#3067)
* Fix macOS logs and add `DD_INTERNAL_NATIVE_LOADER_PATH` env var (#3069)
* Zippy1981/add note to update documentation when you add an instrumentation (#3080)


[Changes since 2.13.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.13.0...v2.14.0)


## [Release 2.13.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.13.0)

## Summary

- The `http.route`, http.url`, `http.useragenet`, and `http.client_ip` tags have been standardised
  - As part of this change, the (obfuscated) querystring is now reported in `http.url`. Set `DD_HTTP_SERVER_TAG_QUERY_STRING=false` to disable querystring collection.
- Fixes a bug where `x-datadog-tags` propagation header was added even when empty or disabled
- Add support for continuous profiler on Cent0S7

## Changes

### Tracer
* [Tracer] Fix span properties (#2918)
* [Tracer][AAS] Add Resource Id on chunks to avoid double billing (#2960)
* Add support for Multipart form data to ApiWebRequest and HttpClientRequest (#2962)
* Add FNV-64bit hash implementation for Data Streams Monitoring (#2973)
* [Tracer] Do not add x-datadog-tags header when empty or disabled (#3001)
* [Profiler] Fix Arm64 installer smoke tests (#3002)

### ASM
* [ASM] Tags standardization, http.route, http.url, http.useragent, http.client_ip (#2915)
* Non-backtracking regex from .NET 7.0 (#3009, #3022, #3025)

### Continuous Profiler
* [Profiler] Refactor raw sample processing (#2917)
* [Profiler] Ensure the profiler run on CentOS7 (#2950)
* [Profiler] Deactivate profiling on linux if wrapper library is not used (#2967)
* [Profiler] Bump libddprof to libdatadog (former libddprof) 0.7.0 (#2970)
* [Profiler] Run Profiler unit tests in container in Github Actions (#2975)
* [Profiler] Minor cleanup (#2977)
* [Profiler] Remove shared managed code (#2978)
* [Profiler] Use net6.0 for integration tests (#2987)
* [Profiler] Deactivate profiler on ARM64 (#2994)

### Debugger
* [Debugger] Introducing the Live Debugger product (#2965)
* Temporarily disable Live Debugger (#3012)
* Increase WaitForExit time in `DebuggerSampleProcessHelper` (#3013)

### Build / Test
* Installer test tweaks (#2963)
* [Test Package Versions Bump] Updating package versions (#2972)
* Update vendored version of Newtonsoft.Json (#2980)
* Fix Aerospike tests and convert to snapshots (#2985)
* Remove confusing DD_HEADER_TAGS configuration in OwinWebApi2Tests (#2986)
* Ignore `process_id` in smoke test snapshots on Linux (#3003)
* [CI] Allow throughput to run on benchmark branches (#3005)
* Fix the path to the native loader (#3006)
* Try disabling telemetry in ci (#3010)
* [Tracer] Add Fluent API to validate integration spans and produce documentation of tracing integrations -- Redo (#3011)
* Downgrade version of `dotenv` installed with `fpm` (#3014)
* Use macos-11 for builds instead of macos-10.15 as that is deprecated (#3035)

### Miscellaneous
* [CI Visibility] - Add support for CoveragePayload message pack serialization (#2969)
* [CI Visibility] Send Code Coverage Payload to the agentless endpoint (#2983)


[Changes since 2.12.0](https://github.com/DataDog/dd-trace-dotnet/compare/v2.12.0...v2.13.0)


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
