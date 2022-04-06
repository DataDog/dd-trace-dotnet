// <copyright file="BuggyBitsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
using Perftools.Profiles.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.CodeHotspot
{
    public class BuggyBitsTest
    {
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

            var profilerRuntimeId = string.Empty;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                var rid = ctx.Value.Request.Headers["runtime-id"];
                Assert.NotNull(rid);

                if (profilerRuntimeId != string.Empty)
                {
                    Assert.True(profilerRuntimeId == rid);
                }
                else
                {
                    profilerRuntimeId = rid;
                }
            };

            var tracerRuntimeId = string.Empty;
            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                var rid = ctx.Value.Request.Headers["runtime-id"];
                Assert.NotNull(rid);

                if (tracerRuntimeId != string.Empty)
                {
                    Assert.True(tracerRuntimeId == rid);
                }
                else
                {
                    tracerRuntimeId = rid;
                }
            };

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

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
