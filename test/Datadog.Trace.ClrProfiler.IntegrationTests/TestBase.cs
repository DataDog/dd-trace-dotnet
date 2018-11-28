using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TestBase
    {
        protected TestBase(string sampleAppName, ITestOutputHelper output)
        {
            SampleAppName = sampleAppName;
            Output = output;

            Output.WriteLine($"Platform: {TestHelper.GetPlatform()}");
            Output.WriteLine($"Configuration: {BuildParameters.Configuration}");
            Output.WriteLine($"TargetFramework: {BuildParameters.TargetFramework}");
            Output.WriteLine($".NET Core: {BuildParameters.CoreClr}");
            Output.WriteLine($"Application: {TestHelper.GetSampleApplicationPath(sampleAppName)}");
            Output.WriteLine($"Profiler DLL: {TestHelper.GetProfilerDllPath()}");
        }

        protected string SampleAppName { get; }

        protected ITestOutputHelper Output { get; }

        public ProcessResult RunSampleApp(int traceAgentPort, string arguments = null)
        {
            // get path to native profiler dll
            string profilerDllPath = TestHelper.GetProfilerDllPath();

            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            // get path to sample app that the profiler will attach to
            string sampleAppPath = TestHelper.GetSampleApplicationPath(SampleAppName);

            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            Process process = ProfilerHelper.StartProcessWithProfiler(
                sampleAppPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath,
                arguments,
                traceAgentPort);

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

        protected async Task AssertHttpSpan(
            string path,
            int agentPort,
            int httpPort,
            HttpStatusCode expectedHttpStatusCode,
            string expectedSpanType,
            string expectedOperationName,
            string expectedResourceName)
        {
            List<MockTracerAgent.Span> spans;

            using (var agent = new MockTracerAgent(agentPort))
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync($"http://localhost:{httpPort}" + path);
                var content = await response.Content.ReadAsStringAsync();
                Output.WriteLine($"[http] {response.StatusCode}{Environment.NewLine}{content}");
                Assert.Equal(expectedHttpStatusCode, response.StatusCode);

                spans = agent.WaitForSpans(1);
                Assert.True(spans.Count == 1, "expected one span");
            }

            MockTracerAgent.Span span = spans[0];
            Assert.Equal(expectedSpanType, span.Type);
            Assert.Equal(expectedOperationName, span.Name);
            Assert.Equal(expectedResourceName, span.Resource);
        }
    }
}
