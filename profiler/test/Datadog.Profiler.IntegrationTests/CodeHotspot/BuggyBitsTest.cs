// <copyright file="BuggyBitsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;
using MessagePack;
using Perftools.Profiles.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.CodeHotspot
{
    public class BuggyBitsTest
    {
        private static readonly Regex RuntimeIdPattern = new("runtime-id:(?<runtimeId>[A-Z0-9-]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private readonly ITestOutputHelper _output;

        public BuggyBitsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Datadog.Demos.BuggyBits", DisplayName = "BuggyBits", UseNativeLoader = true)]
        public void CheckSpanContextAreAttached(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableNewPipeline: true, enableTracer: true);
            runner.Environment.SetVariable(EnvironmentVariables.CodeHotSpotsEnable, "1");

            using var agent = new MockDatadogAgent(_output);

            var profilerRuntimeIds = new List<string>();
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                profilerRuntimeIds.Add(ExtractRuntimeIdFromProfilerRequest(ctx.Value.Request));
            };

            var tracerRuntimeIds = new List<string>();
            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                tracerRuntimeIds.AddRange(ExtractRuntimeIdsFromTracerRequest(ctx.Value.Request));
            };

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            Assert.Single(profilerRuntimeIds.Distinct());
            Assert.Single(tracerRuntimeIds.Distinct());

            var profilerRuntimeId = profilerRuntimeIds.First();

            Assert.NotNull(profilerRuntimeId);

            var tracerRuntimeId = tracerRuntimeIds.First();
            Assert.NotNull(tracerRuntimeId);

            Assert.Equal(profilerRuntimeId, tracerRuntimeId);

            var tracingContexts = GetTracingContexts(runner.Environment.PprofDir);
            Assert.NotEmpty(tracingContexts);
            Assert.All(tracingContexts, (PprofHelper.Label label) => Assert.False(string.IsNullOrWhiteSpace(label.Value)));
        }

        [TestAppFact("Datadog.Demos.BuggyBits", DisplayName = "BuggyBits", UseNativeLoader = true)]
        public void NoTraceContextAttachedIfFeatureDeactivated(string appName, string framework, string appAssembly)
        {
            using var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableNewPipeline: true, enableTracer: true);
            // We do not set the environment variable to activate the feature

            using var agent = new MockDatadogAgent(_output);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var tracingContexts = GetTracingContexts(runner.Environment.PprofDir);
            Assert.Empty(tracingContexts);
        }

        private static HashSet<string> ExtractRuntimeIdsFromTracerRequest(HttpListenerRequest request)
        {
            var traces = MessagePackSerializer.Deserialize<IList<IList<MockSpan>>>(request.InputStream);

            var result = new HashSet<string>();
            foreach (var trace in traces)
            {
                foreach (var span in trace)
                {
                    var currentRuntimeId = string.Empty;
                    if (span.Tags?.TryGetValue(Tags.RuntimeId, out currentRuntimeId) ?? false)
                    {
                        result.Add(currentRuntimeId);
                    }
                }
            }

            return result;
        }

        private static string ExtractRuntimeIdFromProfilerRequest(HttpListenerRequest request)
        {
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            var match = RuntimeIdPattern.Match(text);
            return match.Groups["runtimeId"].Value;
        }

        private static List<PprofHelper.Label> GetTracingContexts(string pprofDir)
        {
            var tracingContext = new List<PprofHelper.Label>();
            foreach (var file in Directory.EnumerateFiles(pprofDir, "*.pprof", SearchOption.AllDirectories))
            {
                tracingContext.AddRange(ExtractTracingContext(file));
            }

            return tracingContext;
        }

        private static IEnumerable<PprofHelper.Label> ExtractTracingContext(string file)
        {
            using var s = File.OpenRead(file);
            var profile = Profile.Parser.ParseFrom(s);

            return profile.Labels().Where(k => k.Name == "local root span id" || k.Name == "span id");
        }
    }
}
