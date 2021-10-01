// <copyright file="TestHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

#if NETFRAMEWORK
using Datadog.Trace.ExtensionMethods; // needed for Dictionary<K,V>.GetValueOrDefault()
#endif

namespace Datadog.Trace.TestHelpers
{
    public abstract class TestHelper
    {
        protected TestHelper(string sampleAppName, string samplePathOverrides)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), samplePathOverrides))
        {
        }

        protected TestHelper(string sampleAppName)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper)))
        {
        }

        protected TestHelper(EnvironmentHelper environmentHelper)
        {
            EnvironmentHelper = environmentHelper;

            Console.WriteLine($"Platform: {EnvironmentTools.GetPlatform()}");
            Console.WriteLine($"Configuration: {EnvironmentTools.GetBuildConfiguration()}");
            Console.WriteLine($"TargetFramework: {EnvironmentHelper.GetTargetFramework()}");
            Console.WriteLine($".NET Core: {EnvironmentHelper.IsCoreClr()}");
            Console.WriteLine($"Profiler DLL: {EnvironmentHelper.GetProfilerPath()}");
        }

        protected EnvironmentHelper EnvironmentHelper { get; }

        protected string TestPrefix => $"{EnvironmentTools.GetBuildConfiguration()}.{EnvironmentHelper.GetTargetFramework()}";

        public Process StartDotnetTestSample(int traceAgentPort, string arguments, string packageVersion, int aspNetCorePort, int? statsdPort = null, string framework = "")
        {
            // get path to sample app that the profiler will attach to
            string sampleAppPath = EnvironmentHelper.GetTestCommandForSampleApplicationPath(packageVersion, framework);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            Console.WriteLine($"Starting Application: {sampleAppPath}");
            string testCli = EnvironmentHelper.GetDotNetTest();
            string exec = testCli;
            string appPath = testCli.StartsWith("dotnet") ? $"vstest {sampleAppPath}" : sampleAppPath;
            Console.WriteLine("Executable: " + exec);
            Console.WriteLine("ApplicationPath: " + appPath);
            return ProfilerHelper.StartProcessWithProfiler(
                exec,
                EnvironmentHelper,
                $"{appPath} {arguments ?? string.Empty}",
                traceAgentPort: traceAgentPort,
                statsdPort: statsdPort,
                aspNetCorePort: aspNetCorePort,
                processToProfile: exec);
        }

        public ProcessResult RunDotnetTestSampleAndWaitForExit(int traceAgentPort, int? statsdPort = null, string arguments = null, string packageVersion = "", string framework = "")
        {
            var process = StartDotnetTestSample(traceAgentPort, arguments, packageVersion, aspNetCorePort: 5000, statsdPort: statsdPort, framework: framework);

            using var helper = new ProcessHelper(process);

            process.WaitForExit();
            helper.Drain();
            var exitCode = process.ExitCode;

            Console.WriteLine($"ProcessId: " + process.Id);
            Console.WriteLine($"Exit Code: " + exitCode);

            var standardOutput = helper.StandardOutput;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Console.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            var standardError = helper.ErrorOutput;

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Console.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }

        public Process StartSample(int traceAgentPort, string arguments, string packageVersion, int aspNetCorePort, int? statsdPort = null, string framework = "")
        {
            // get path to sample app that the profiler will attach to
            string sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            Console.WriteLine($"Starting Application: {sampleAppPath}");
            var executable = EnvironmentHelper.IsCoreClr() ? EnvironmentHelper.GetSampleExecutionSource() : sampleAppPath;
            var args = EnvironmentHelper.IsCoreClr() ? $"{sampleAppPath} {arguments ?? string.Empty}" : arguments;

            return ProfilerHelper.StartProcessWithProfiler(
                executable,
                EnvironmentHelper,
                args,
                traceAgentPort: traceAgentPort,
                statsdPort: statsdPort,
                aspNetCorePort: aspNetCorePort,
                processToProfile: executable);
        }

        public ProcessResult RunSampleAndWaitForExit(int traceAgentPort, int? statsdPort = null, string arguments = null, string packageVersion = "", string framework = "", int aspNetCorePort = 5000)
        {
            var process = StartSample(traceAgentPort, arguments, packageVersion, aspNetCorePort: aspNetCorePort, statsdPort: statsdPort, framework: framework);

            using var helper = new ProcessHelper(process);

            process.WaitForExit();
            helper.Drain();
            var exitCode = process.ExitCode;

            Console.WriteLine($"ProcessId: " + process.Id);
            Console.WriteLine($"Exit Code: " + exitCode);

            var standardOutput = helper.StandardOutput;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Console.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            var standardError = helper.ErrorOutput;

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Console.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

#if NETCOREAPP2_1
            if (exitCode == 139)
            {
                // Segmentation faults are expected on .NET Core because of a bug in the runtime: https://github.com/dotnet/runtime/issues/11885
                throw new InconclusiveException("Segmentation fault on .NET Core 2.1");
            }
#endif

            Assert.True(exitCode >= 0, $"Process exited with code {exitCode}");

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }

        public (Process Process, string ConfigFile) StartIISExpress(int traceAgentPort, int iisPort, IisAppType appType)
        {
            var iisExpress = EnvironmentHelper.GetIisExpressPath();

            var appPool = appType switch
            {
                IisAppType.AspNetClassic => "Clr4ClassicAppPool",
                IisAppType.AspNetIntegrated => "Clr4IntegratedAppPool",
                IisAppType.AspNetCoreInProcess => "UnmanagedClassicAppPool",
                IisAppType.AspNetCoreOutOfProcess => "UnmanagedClassicAppPool",
                _ => throw new InvalidOperationException($"Unknown {nameof(IisAppType)} '{appType}'"),
            };

            var appPath = appType switch
            {
                IisAppType.AspNetClassic => EnvironmentHelper.GetSampleProjectDirectory(),
                IisAppType.AspNetIntegrated => EnvironmentHelper.GetSampleProjectDirectory(),
                IisAppType.AspNetCoreInProcess => EnvironmentHelper.GetSampleApplicationOutputDirectory(),
                IisAppType.AspNetCoreOutOfProcess => EnvironmentHelper.GetSampleApplicationOutputDirectory(),
                _ => throw new InvalidOperationException($"Unknown {nameof(IisAppType)} '{appType}'"),
            };

            var configTemplate = File.ReadAllText("applicationHost.config");

            var newConfig = Path.GetTempFileName();

            configTemplate = configTemplate
                            .Replace("[PATH]", appPath)
                            .Replace("[PORT]", iisPort.ToString())
                            .Replace("[POOL]", appPool);

            var isAspNetCore = appType == IisAppType.AspNetCoreInProcess || appType == IisAppType.AspNetCoreOutOfProcess;
            if (isAspNetCore)
            {
                var hostingModel = appType == IisAppType.AspNetCoreInProcess ? "inprocess" : "outofprocess";
                configTemplate = configTemplate
                                .Replace("[DOTNET]", EnvironmentHelper.GetDotnetExe())
                                .Replace("[RELATIVE_SAMPLE_PATH]", $".\\{EnvironmentHelper.GetSampleApplicationFileName()}")
                                .Replace("[HOSTING_MODEL]", hostingModel);
            }

            File.WriteAllText(newConfig, configTemplate);

            var args = new[]
                {
                    "/site:sample",
                    $"/config:{newConfig}",
                    "/systray:false",
                    "/trace:info"
                };

            Console.WriteLine($"[webserver] starting {iisExpress} {string.Join(" ", args)}");

            var process = ProfilerHelper.StartProcessWithProfiler(
                iisExpress,
                EnvironmentHelper,
                arguments: string.Join(" ", args),
                redirectStandardInput: true,
                traceAgentPort: traceAgentPort,
                processToProfile: appType == IisAppType.AspNetCoreOutOfProcess ? "dotnet.exe" : iisExpress);

            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    Console.WriteLine($"[webserver][stdout] {line}");

                    if (line.Contains("IIS Express is running"))
                    {
                        wh.Set();
                    }
                }
            });

            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardError.ReadLine()) != null)
                {
                    Console.WriteLine($"[webserver][stderr] {line}");
                }
            });

            wh.WaitOne(5000);

            // Wait for iis express to finish starting up
            var retries = 5;
            while (true)
            {
                var usedPorts = IPGlobalProperties.GetIPGlobalProperties()
                                                  .GetActiveTcpListeners()
                                                  .Select(ipEndPoint => ipEndPoint.Port);

                if (usedPorts.Contains(iisPort))
                {
                    break;
                }

                retries--;

                if (retries == 0)
                {
                    throw new Exception("Gave up waiting for IIS Express.");
                }

                Thread.Sleep(1500);
            }

            return (process, newConfig);
        }

        protected void ValidateSpans<T>(IEnumerable<MockTracerAgent.Span> spans, Func<MockTracerAgent.Span, T> mapper, IEnumerable<T> expected)
        {
            var spanLookup = new Dictionary<T, int>();
            foreach (var span in spans)
            {
                var key = mapper(span);
                if (spanLookup.ContainsKey(key))
                {
                    spanLookup[key]++;
                }
                else
                {
                    spanLookup[key] = 1;
                }
            }

            var missing = new List<T>();
            foreach (var e in expected)
            {
                var found = spanLookup.ContainsKey(e);
                if (found)
                {
                    if (--spanLookup[e] <= 0)
                    {
                        spanLookup.Remove(e);
                    }
                }
                else
                {
                    missing.Add(e);
                }
            }

            foreach (var e in missing)
            {
                Assert.True(false, $"no span found for `{e}`, remaining spans: `{string.Join(", ", spanLookup.Select(kvp => $"{kvp.Key}").ToArray())}`");
            }
        }

        protected void EnableDebugMode()
        {
            EnvironmentHelper.DebugModeEnabled = true;
        }

        protected void SetEnvironmentVariable(string key, string value)
        {
            EnvironmentHelper.CustomEnvironmentVariables.Add(key, value);
        }

        protected void SetServiceVersion(string serviceVersion)
        {
            SetEnvironmentVariable("DD_VERSION", serviceVersion);
        }

        protected void SetCallTargetSettings(bool enableCallTarget)
        {
            SetEnvironmentVariable("DD_TRACE_CALLTARGET_ENABLED", enableCallTarget ? "true" : "false");
        }

        protected void SetSecurity(bool security)
        {
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSecEnabled, security ? "true" : "false");
        }

        protected void SetAppSecBlockingEnabled(bool appSecBlockingEnabled)
        {
            SetEnvironmentVariable(Configuration.ConfigurationKeys.AppSecBlockingEnabled, appSecBlockingEnabled ? "true" : "false");
        }

#if !NET452
        protected async Task<IImmutableList<MockTracerAgent.Span>> GetWebServerSpans(
            string path,
            MockTracerAgent agent,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            int expectedSpanCount = 2,
            bool filterServerSpans = true)
        {
            using var httpClient = new HttpClient();

            // disable tracing for this HttpClient request
            httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            var testStart = DateTime.UtcNow;
            var response = await httpClient.GetAsync($"http://localhost:{httpPort}" + path);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[http] {response.StatusCode} {content}");
            Assert.AreEqual(expectedHttpStatusCode, response.StatusCode);

            if (filterServerSpans)
            {
                agent.SpanFilters.Add(IsServerSpan);
            }

            return agent.WaitForSpans(
                count: expectedSpanCount,
                minDateTime: testStart,
                returnAllOperations: true);
        }
#endif

        protected async Task AssertWebServerSpan(
            string path,
            MockTracerAgent agent,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            bool isError,
            string expectedAspNetErrorType,
            string expectedAspNetErrorMessage,
            string expectedErrorType,
            string expectedErrorMessage,
            string expectedSpanType,
            string expectedOperationName,
            string expectedAspNetResourceName,
            string expectedResourceName,
            string expectedServiceVersion,
            IReadOnlyDictionary<string, string> expectedTags = null)
        {
            IImmutableList<MockTracerAgent.Span> spans;

            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                var testStart = DateTime.UtcNow;
                var response = await httpClient.GetAsync($"http://localhost:{httpPort}" + path);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.AreEqual(expectedHttpStatusCode, response.StatusCode);

                agent.SpanFilters.Add(IsServerSpan);

                spans = agent.WaitForSpans(
                    count: 2,
                    minDateTime: testStart,
                    returnAllOperations: true);

                Assert.True(spans.Count == 2, $"expected two span, saw {spans.Count}");
            }

            MockTracerAgent.Span aspnetSpan = spans.Where(s => s.Name == "aspnet.request").FirstOrDefault();
            MockTracerAgent.Span innerSpan = spans.Where(s => s.Name == expectedOperationName).FirstOrDefault();

            Assert.NotNull(aspnetSpan);
            Assert.AreEqual(expectedAspNetResourceName, aspnetSpan.Resource);

            Assert.NotNull(innerSpan);
            Assert.AreEqual(expectedResourceName, innerSpan.Resource);

            foreach (MockTracerAgent.Span span in spans)
            {
                // base properties
                Assert.AreEqual(expectedSpanType, span.Type);

                // errors
                Assert.AreEqual(isError, span.Error == 1);
                if (span == aspnetSpan)
                {
                    Assert.AreEqual(expectedAspNetErrorType, span.Tags.GetValueOrDefault(Tags.ErrorType));
                    Assert.AreEqual(expectedAspNetErrorMessage, span.Tags.GetValueOrDefault(Tags.ErrorMsg));
                }
                else if (span == innerSpan)
                {
                    Assert.AreEqual(expectedErrorType, span.Tags.GetValueOrDefault(Tags.ErrorType));
                    Assert.AreEqual(expectedErrorMessage, span.Tags.GetValueOrDefault(Tags.ErrorMsg));
                }

                // other tags
                Assert.AreEqual(SpanKinds.Server, span.Tags.GetValueOrDefault(Tags.SpanKind));
                Assert.AreEqual(expectedServiceVersion, span.Tags.GetValueOrDefault(Tags.Version));
            }

            if (expectedTags?.Values is not null)
            {
                foreach (var expectedTag in expectedTags)
                {
                    Assert.AreEqual(expectedTag.Value, innerSpan.Tags.GetValueOrDefault(expectedTag.Key));
                }
            }
        }

        protected async Task AssertAspNetSpanOnly(
            string path,
            MockTracerAgent agent,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage,
            string expectedSpanType,
            string expectedResourceName,
            string expectedServiceVersion)
        {
            IImmutableList<MockTracerAgent.Span> spans;

            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                var testStart = DateTime.UtcNow;
                var response = await httpClient.GetAsync($"http://localhost:{httpPort}" + path);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.AreEqual(expectedHttpStatusCode, response.StatusCode);

                spans = agent.WaitForSpans(
                    count: 1,
                    minDateTime: testStart,
                    operationName: "aspnet.request",
                    returnAllOperations: true);

                Assert.True(spans.Count == 1, $"expected two span, saw {spans.Count}");
            }

            MockTracerAgent.Span span = spans[0];

            // base properties
            Assert.AreEqual(expectedResourceName, span.Resource);
            Assert.AreEqual(expectedSpanType, span.Type);

            // errors
            Assert.AreEqual(isError, span.Error == 1);
            Assert.AreEqual(expectedErrorType, span.Tags.GetValueOrDefault(Tags.ErrorType));
            Assert.AreEqual(expectedErrorMessage, span.Tags.GetValueOrDefault(Tags.ErrorMsg));

            // other tags
            Assert.AreEqual(SpanKinds.Server, span.Tags.GetValueOrDefault(Tags.SpanKind));
            Assert.AreEqual(expectedServiceVersion, span.Tags.GetValueOrDefault(Tags.Version));
        }

        private bool IsServerSpan(MockTracerAgent.Span span) =>
            span.Tags.GetValueOrDefault(Tags.SpanKind) == SpanKinds.Server;

        protected internal class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                Add(new Tuple<T1, T2>(item, item2));
            }
        }
    }
}
