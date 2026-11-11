// <copyright file="CodeHotspotTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using MessagePack;
using Perftools.Profiles;
using Xunit;
using Xunit.Abstractions;
using ProfilerXunit = Datadog.Profiler.IntegrationTests.Xunit;

namespace Datadog.Profiler.IntegrationTests.CodeHotspot
{
    public class CodeHotspotTest
    {
        private const string ScenarioCodeHotspot = "--scenario 256";
        private const string ScenarioEndpoint = "--scenario 8192";
        private static readonly Regex RuntimeIdPattern = new("runtime-id:(?<runtimeId>[A-Z0-9-]+)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private readonly ITestOutputHelper _output;

        public CodeHotspotTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckTraceContextAreAttachedForWalltimeProfilerHumberOfThreads(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot + " --with-idle-threads 500");
            // By default, the codehotspot feature is activated

            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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
            Assert.NotEmpty(profilerRuntimeId);

            var tracerRuntimeId = tracerRuntimeIds.First();
            Assert.NotNull(tracerRuntimeId);
            Assert.NotEmpty(tracerRuntimeId);

            Assert.Equal(profilerRuntimeId, tracerRuntimeId);

            // We cannot enumerate and check for each pprof files if it contains trace context.
            // The profiler is configured to export/write pprof file every 3s, but
            // depending on the machine or if it's release or debug, the first pprof file
            // may not contains any trace context.
            var tracingContexts = GetTracingContextsFromPprofFiles(runner.Environment.PprofDir);
            Assert.NotEmpty(tracingContexts);
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSpanContextAreAttachedForWalltimeProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot);
            // By default, the codehotspot feature is activated

            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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
            Assert.NotEmpty(profilerRuntimeId);

            var tracerRuntimeId = tracerRuntimeIds.First();
            Assert.NotNull(tracerRuntimeId);
            Assert.NotEmpty(tracerRuntimeId);

            Assert.Equal(profilerRuntimeId, tracerRuntimeId);

            // We cannot enumerate and check for each pprof files if it contains trace context.
            // The profiler is configured to export/write pprof file every 3s, but
            // depending on the machine or if it's release or debug, the first pprof file
            // may not contains any trace context.
            var tracingContexts = GetTracingContextsFromPprofFiles(runner.Environment.PprofDir);
            Assert.NotEmpty(tracingContexts);
        }

        [TestAppFact("Samples.BuggyBits", Frameworks = new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0", "net11.0" })]
        public void CheckProfilingWorksWhenTracingDisabled(string appName, string framework, string appAssembly)
        {
            // Production-style configuration: both native loader components (PROFILER and TRACER) are loaded,
            // the managed SDK is loaded and initialized, and the SDK configures the native profiler through
            // Stable Configuration while span generation is disabled.
            // DD_PROFILING_MANAGED_ACTIVATION_ENABLED is deliberately left at its default (enabled):
            // profiling must be activated by the managed layer, not by the native kill switch.
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot);

            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable("DD_TRACE_ENABLED", "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            var traceRequestCount = 0;
            agent.TracerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                Interlocked.Increment(ref traceRequestCount);
            };

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0, $"Expected at least one profiling request. Log directory: {runner.Environment.LogDir}");
            Assert.True(SamplesHelper.GetSamplesCount(runner.Environment.PprofDir) > 0, $"Expected exported profiles to contain samples. Pprof directory: {runner.Environment.PprofDir}");
            Assert.True(Volatile.Read(ref traceRequestCount) == 0, $"Expected no trace requests while tracing is disabled. Pprof directory: {runner.Environment.PprofDir}");

            // The successful handshake with the managed layer is observable evidence that the managed SDK was
            // initialized and that the native profiler accepted its configuration with managed activation enabled.
            AssertProfilingLogsContains(runner.Environment.LogDir, "Managed layer provides Stable Configuration.");
            AssertProfilingLogsNotContains(runner.Environment.LogDir, "Managed layer provides Stable Configuration even when managed activation is disabled.");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void CheckSpanContextAreAttachedForCpuProfiler(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot);
            // By default, the codehotspot feature is activated
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "ManualCpuTime");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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
            Assert.NotEmpty(profilerRuntimeId);

            var tracerRuntimeId = tracerRuntimeIds.First();
            Assert.NotNull(tracerRuntimeId);
            Assert.NotEmpty(tracerRuntimeId);

            Assert.Equal(profilerRuntimeId, tracerRuntimeId);

            var tracingContexts = GetTracingContextsFromPprofFiles(runner.Environment.PprofDir);
            Assert.NotEmpty(tracingContexts);

            // In the first versions of this test, we extracted span ids from Tracer requests and ensured that the ones collected by the
            // profiler was a subset. But this makes the test flacky: not flushed when the application is closing.
        }

        [TestAppFact("Samples.BuggyBits")]
        [Trait("Category", "LinuxOnly")]
        public void CheckSpanContextAreAttachedForCpuProfiler_TimerCreate(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot);
            // By default, the codehotspot feature is activated
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerType, "TimerCreate");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

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

            CpuProfilerHelper.SkipIfTimerCreateWasDowngraded(runner.Environment.LogDir);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            Assert.Single(profilerRuntimeIds.Distinct());
            Assert.Single(tracerRuntimeIds.Distinct());

            var profilerRuntimeId = profilerRuntimeIds.First();
            Assert.NotNull(profilerRuntimeId);
            Assert.NotEmpty(profilerRuntimeId);

            var tracerRuntimeId = tracerRuntimeIds.First();
            Assert.NotNull(tracerRuntimeId);
            Assert.NotEmpty(tracerRuntimeId);

            Assert.Equal(profilerRuntimeId, tracerRuntimeId);

            var tracingContexts = GetTracingContextsFromPprofFiles(runner.Environment.PprofDir);
            Assert.NotEmpty(tracingContexts);

            // In the first versions of this test, we extracted span ids from Tracer requests and ensured that the ones collected by the
            // profiler was a subset. But this makes the test flacky: not flushed when the application is closing.
        }

        [TestAppFact("Samples.BuggyBits")]
        public void NoTraceContextAttachedIfFeatureDeactivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioCodeHotspot);
            runner.Environment.SetVariable(EnvironmentVariables.CodeHotSpotsEnable, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var tracingContexts = GetTracingContextsFromPprofFiles(runner.Environment.PprofDir);
            Assert.Empty(tracingContexts);
        }

        [ProfilerXunit.Flaky("Endpoint association can race with the profiler's shutdown export on slow/32-bit runtimes")]
        [TestAppFact("Samples.BuggyBits")]
        public void CheckEndpointsAreAttached(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioEndpoint);
            runner.TestDurationInSeconds = 20;

            // By default, the endpoint profiling feature is activated

            // The "trace endpoint" label is attached to a sample only when (a) the web root span closes (the tracer
            // then calls SetEndpointForTrace) and (b) a sample carries that span's "local root span id", both within
            // the SAME profile generation. Use a purpose-built endpoint that burns CPU for a short bounded interval and
            // returns a small response: it is reliably sampled with a trace context, completes with plenty of margin
            // before shutdown, and avoids the exception storm from the BuggyBits sales endpoint that can crash Alpine
            // CI runs while the default exception profiler is enabled. A single export window (flushed on shutdown)
            // keeps the samples and the endpoints in the same generation.
            runner.Environment.SetVariable("DD_PROFILING_UPLOAD_PERIOD", "600");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var endpoints = GetEndpointsFromPprofFiles(runner.Environment.PprofDir);

            endpoints.Distinct().Should().BeEquivalentTo("GET /products/endpointprofiling");
        }

        [TestAppFact("Samples.BuggyBits")]
        public void NoEndpointsAttachedIfFeatureDeactivated(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, enableTracer: true, commandLine: ScenarioEndpoint);
            runner.TestDurationInSeconds = 20;
            runner.Environment.SetVariable(EnvironmentVariables.EndpointProfilerEnabled, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);

            runner.Run(agent);

            Assert.True(agent.NbCallsOnProfilingEndpoint > 0);

            var endpoints = GetEndpointsFromPprofFiles(runner.Environment.PprofDir).Distinct();

            endpoints.Should().BeEmpty();
        }

        private static void AssertProfilingLogsContains(string logDir, string expectedContent)
        {
            var logFile = GetProfilerLogFile(logDir);
            var found = false;
            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains(expectedContent))
                {
                    found = true;
                }
            }

            Assert.True(found, $"No log line contains: {expectedContent} (log file: {logFile})");
        }

        private static void AssertProfilingLogsNotContains(string logDir, string unexpectedContent)
        {
            var logFile = GetProfilerLogFile(logDir);
            var found = false;
            foreach (var line in File.ReadLines(logFile))
            {
                if (line.Contains(unexpectedContent))
                {
                    found = true;
                }
            }

            Assert.False(found, $"Log should not contain: {unexpectedContent} (log file: {logFile})");
        }

        private static string GetProfilerLogFile(string logDir)
        {
            return Directory.GetFiles(logDir)
                            .Single(f => Path.GetFileName(f).StartsWith("DD-DotNet-Profiler-Native-"));
        }

        private static HashSet<string> ExtractRuntimeIdsFromTracerRequest(HttpListenerRequest request)
        {
            var traces = MessagePackSerializer.Deserialize<List<List<MockSpan>>>(request.InputStream);

            var runtimeIds = new HashSet<string>();
            foreach (var trace in traces)
            {
                foreach (var span in trace)
                {
                    var currentRuntimeId = string.Empty;
                    if (span.Tags?.TryGetValue(Tags.RuntimeId, out currentRuntimeId) ?? false)
                    {
                        runtimeIds.Add(currentRuntimeId);
                    }
                }
            }

            return runtimeIds;
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

        private static IEnumerable<string> GetEndpointsFromPprofFiles(string pprofDir)
        {
            foreach (var profile in SamplesHelper.GetProfiles(pprofDir))
            {
                foreach (var label in profile.Labels().SelectMany(_ => _))
                {
                    if (label.Name == "trace endpoint")
                    {
                        yield return label.Value;
                    }
                }
            }
        }

        private static List<(ulong LocalRootSpanId, ulong SpanId)> GetTracingContextsFromPprofFiles(string pprofDir)
        {
            var tracingContext = new List<(ulong LocalRootSpanId, ulong SpanId)>();
            foreach (var profile in SamplesHelper.GetProfiles(pprofDir))
            {
                tracingContext.AddRange(ExtractTracingContext(profile));
            }

            return tracingContext;
        }

        private static IEnumerable<(ulong LocalRootSpanId, ulong SpanId)> ExtractTracingContext(Profile profile)
        {
            foreach (var labelsPerSample in profile.Labels())
            {
                ulong localRootSpanId = 0;
                ulong spanId = 0;

                foreach (var label in labelsPerSample)
                {
                    if (label.Name == "local root span id")
                    {
                        localRootSpanId = (ulong)long.Parse(label.Value);
                    }

                    if (label.Name == "span id")
                    {
                        spanId = (ulong)long.Parse(label.Value);
                    }
                }

                if (spanId != 0 && localRootSpanId != 0)
                {
                    yield return (localRootSpanId, spanId);
                }
            }
        }
    }
}
