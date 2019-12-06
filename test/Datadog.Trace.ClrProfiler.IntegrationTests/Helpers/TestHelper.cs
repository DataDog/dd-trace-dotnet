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
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TestHelper
    {
        protected TestHelper(string sampleAppName, string samplePathOverrides, ITestOutputHelper output, string disabledIntegrations = null)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, samplePathOverrides, disabledIntegrations), output)
        {
        }

        protected TestHelper(string sampleAppName, ITestOutputHelper output, string disabledIntegrations = null)
            : this(new EnvironmentHelper(sampleAppName, typeof(TestHelper), output, disabledIntegrations), output)
        {
        }

        protected TestHelper(EnvironmentHelper environmentHelper, ITestOutputHelper output)
        {
            EnvironmentHelper = environmentHelper;
            SampleAppName = EnvironmentHelper.SampleName;
            Output = output;

            PathToSample = EnvironmentHelper.GetSampleApplicationOutputDirectory();
            Output.WriteLine($"Platform: {EnvironmentHelper.GetPlatform()}");
            Output.WriteLine($"Configuration: {EnvironmentHelper.GetBuildConfiguration()}");
            Output.WriteLine($"TargetFramework: {EnvironmentHelper.GetTargetFramework()}");
            Output.WriteLine($".NET Core: {EnvironmentHelper.IsCoreClr()}");
            Output.WriteLine($"Application: {GetSampleApplicationPath()}");
        }

        protected EnvironmentHelper EnvironmentHelper { get; set; }

        protected string TestPrefix => $"{EnvironmentHelper.GetBuildConfiguration()}.{EnvironmentHelper.GetTargetFramework()}";

        protected string SampleAppName { get; }

        protected string PathToSample { get; }

        protected ITestOutputHelper Output { get; }

        public string GetSampleApplicationPath(string packageVersion = "")
        {
            return EnvironmentHelper.GetSampleApplicationPath(packageVersion);
        }

        public Process StartSample(int traceAgentPort, string arguments, string packageVersion, int aspNetCorePort)
        {
            // get path to sample app that the profiler will attach to
            string sampleAppPath = GetSampleApplicationPath(packageVersion);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            return ProfilerHelper.StartProcessWithProfiler(
                EnvironmentHelper,
                integrationPaths,
                arguments,
                traceAgentPort: traceAgentPort,
                aspNetCorePort: aspNetCorePort);
        }

        public ProcessResult RunSampleAndWaitForExit(int traceAgentPort, string arguments = null, string packageVersion = "")
        {
            Process process = StartSample(traceAgentPort, arguments, packageVersion, aspNetCorePort: 5000);

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            int exitCode = process.ExitCode;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }

        public Process StartIISExpress(int traceAgentPort, int iisPort)
        {
            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            var exe = EnvironmentHelper.GetSampleExecutionSource();
            var args = new string[]
                {
                    $"/clr:v4.0",
                    $"/path:{EnvironmentHelper.GetSampleProjectDirectory()}",
                    $"/systray:false",
                    $"/port:{iisPort}",
                    $"/trace:info",
                };

            Output.WriteLine($"[webserver] starting {exe} {string.Join(" ", args)}");

            var process = ProfilerHelper.StartProcessWithProfiler(
                EnvironmentHelper,
                integrationPaths,
                arguments: string.Join(" ", args),
                redirectStandardInput: true,
                traceAgentPort: traceAgentPort);

            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    Output.WriteLine($"[webserver][stdout] {line}");

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
                    Output.WriteLine($"[webserver][stderr] {line}");
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

            return process;
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

        protected async Task AssertHttpSpan(
            string application,
            string path,
            MockTracerAgent agent,
            HttpStatusCode expectedHttpStatusCode,
            string expectedSpanType,
            string expectedOperationName,
            string expectedResourceName)
        {
            await InternalAssertHttpSpan(
                "http://localhost/" + application,
                path,
                agent,
                expectedHttpStatusCode,
                expectedSpanType,
                expectedOperationName,
                expectedResourceName);
        }

        protected async Task AssertHttpSpan(
            string path,
            MockTracerAgent agent,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            string expectedSpanType,
            string expectedOperationName,
            string expectedResourceName)
        {
            await InternalAssertHttpSpan(
                $"http://localhost:{httpPort}",
                path,
                agent,
                expectedHttpStatusCode,
                expectedSpanType,
                expectedOperationName,
                expectedResourceName);
        }

        protected async Task InternalAssertHttpSpan(
            string application,
            string path,
            MockTracerAgent agent,
            HttpStatusCode expectedHttpStatusCode,
            string expectedSpanType,
            string expectedOperationName,
            string expectedResourceName)
        {
            IImmutableList<MockTracerAgent.Span> spans;

            using (var httpClient = new HttpClient())
            {
                // disable tracing for this HttpClient request
                httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                var testStart = DateTime.UtcNow;
                var response = await httpClient.GetAsync(application + path);
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"[http] {response.StatusCode} {content}");
                Assert.Equal(expectedHttpStatusCode, response.StatusCode);

                spans = agent.WaitForSpans(
                    count: 1,
                    minDateTime: testStart,
                    operationName: expectedOperationName);

                Assert.True(spans.Count == 1, "expected one span");
            }

            MockTracerAgent.Span span = spans[0];
            Assert.Equal(expectedSpanType, span.Type);
            Assert.Equal(expectedOperationName, span.Name);
            Assert.Equal(expectedResourceName, span.Resource);
        }

        internal class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add(T1 item, T2 item2)
            {
                Add(new Tuple<T1, T2>(item, item2));
            }
        }
    }
}
