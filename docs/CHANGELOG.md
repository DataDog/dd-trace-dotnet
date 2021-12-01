# Datadog .NET Tracer (`dd-trace-dotnet`) Release Notes

## [Release 1.31.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.31.0)

## Changes
* [AppSec] Report attacks by the _dd.appsec.triggers tag (#1923)
* [AppSec] Change waf initialization to have better logging (#2068)
* [AppSec] Upgrade WAF rule set (#2086)
* [Appsec] Fix duplicate key in cookies when building waf args (#2098)

## Fixes
* Remove insertion of blocking middleware (#2011)

[Changes since 1.30.1](https://github.com/DataDog/dd-trace-dotnet/compare/v1.30.1...v1.31.0)

## [Release 1.30.1](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.30.1)

## Fixes
* [AppSec] Waf fixes: reading register for waf and unit tests for alpine (#2074)
* Revert "Add modified WCF CallTarget instrumentation via opt-in environment variable" (#2081)

[Changes since 1.30.0](https://github.com/DataDog/dd-trace-dotnet/compare/v1.30.0...v1.30.1)

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
