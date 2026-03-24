// <copyright file="ProcessTagsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;
using MessagePack;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.ProcessTags
{
    public class ProcessTagsTest
    {
        private const string Scenario = "--scenario 2";
        private static readonly Regex ProcessTagsPattern = new("\"process_tags\":\"(?<process_tags>[^\"]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private readonly ITestOutputHelper _output;

        public ProcessTagsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckProcessTagsPropagation(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario, enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED"] = "true";

            var (profilerProcessTags, tracerProcessTags) = RunTest(runner);

            Assert.NotEmpty(profilerProcessTags);
            Assert.NotEmpty(tracerProcessTags);
            Assert.Equal(profilerProcessTags, tracerProcessTags);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckProcessTagsNotPropagatedWhenDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario, enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED"] = "false";

            var (profilerProcessTags, tracerProcessTags) = RunTest(runner);

            Assert.Empty(profilerProcessTags);
            Assert.Empty(tracerProcessTags);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckProcessTagsOnlyInTracerWhenProfilerDisabled(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario, enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED"] = "true";
            runner.Environment.CustomEnvironmentVariables["DD_PROFILING_ENABLED"] = "0";

            var (profilerProcessTags, tracerProcessTags) = RunTest(runner);

            Assert.Empty(profilerProcessTags);
            Assert.NotEmpty(tracerProcessTags);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckProcessTagsContentFormat(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: Scenario, enableTracer: true);
            runner.Environment.CustomEnvironmentVariables["DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED"] = "true";

            var (profilerProcessTags, tracerProcessTags) = RunTest(runner);

            Assert.NotEmpty(profilerProcessTags);

            foreach (var processTags in profilerProcessTags)
            {
                // Should contain at least the basedir tag
                Assert.Contains("entrypoint.basedir:", processTags);
            }
        }

        private static void CollectProfilerProcessTags(HttpListenerRequest request, HashSet<string> profilerProcessTags)
        {
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            var match = ProcessTagsPattern.Match(text);
            var processTags = match.Groups["process_tags"].Value;
            if (string.IsNullOrWhiteSpace(processTags))
            {
                return;
            }

            profilerProcessTags.Add(processTags);
        }

        private static void CollectTracerProcessTags(HttpListenerRequest request, HashSet<string> tracerProcessTags)
        {
            var traces = MessagePackSerializer.Deserialize<List<List<MockSpan>>>(request.InputStream);
            foreach (var trace in traces)
            {
                foreach (var span in trace)
                {
                    var processTagsValue = string.Empty;
                    span.Tags?.TryGetValue(Tags.ProcessTags, out processTagsValue);

                    if (string.IsNullOrWhiteSpace(processTagsValue))
                    {
                        continue;
                    }

                    tracerProcessTags.Add(processTagsValue);
                }
            }
        }

        private (HashSet<string> ProfilerProcessTags, HashSet<string> TracerProcessTags) RunTest(TestApplicationRunner runner)
        {
            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            var profilerProcessTags = new HashSet<string>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectProfilerProcessTags(ctx.Value.Request, profilerProcessTags);
            };

            var tracerProcessTags = new HashSet<string>();
            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                CollectTracerProcessTags(ctx.Value.Request, tracerProcessTags);
            };

            runner.Run(agent);

            return (profilerProcessTags, tracerProcessTags);
        }
    }
}

