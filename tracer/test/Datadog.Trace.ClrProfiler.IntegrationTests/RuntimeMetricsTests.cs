// <copyright file="RuntimeMetricsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(RuntimeMetricsTests), DisableParallelization = true)]
    public class RuntimeMetricsTests : TestHelper
    {
        private readonly ITestOutputHelper _output;

        public RuntimeMetricsTests(ITestOutputHelper output)
            : base("RuntimeMetrics", output)
        {
            SetServiceVersion("1.0.0");
            _output = output;
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task MetricsDisabled()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "0");
            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);

            using var processResult = await RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            Assert.True(requests.Count == 0, "Received metrics despite being disabled. Metrics received: " + string.Join("\n", requests));
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task UdpSubmitsMetrics()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            EnvironmentHelper.EnableDefaultTransport();
            await RunTest();
        }

#if NETCOREAPP3_1_OR_GREATER
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "False")]
        public async Task UdsSubmitsMetrics()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            EnvironmentHelper.EnableUnixDomainSockets();
            await RunTest();
        }
#endif

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task NamedPipesSubmitsMetrics()
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            // The server implementation of named pipes is flaky so have 3 attempts
            var attemptsRemaining = 3;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await RunTest();
                    return;
                }
                catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
                {
                    await ReportRetry(_output, attemptsRemaining, ex);
                }
            }
        }

        private async Task RunTest()
        {
            var inputServiceName = "12_$#Samples.$RuntimeMetrics";
            SetEnvironmentVariable("DD_SERVICE", inputServiceName);
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");
            SetInstrumentationVerification();
            SetEnvironmentVariable("DD_TAGS", "some:value"); // Should be added to the metrics

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using var processResult = await RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            // Check if we receive 2 kinds of metrics:
            // - exception count is gathered using common .NET APIs
            // - contention count is gathered using platform-specific APIs

            var exceptionRequestsCount = requests.Count(r => r.Contains("runtime.dotnet.exceptions.count"));

            Assert.True(exceptionRequestsCount > 0, "No exception metrics received. Metrics received: " + string.Join("\n", requests));

            // Example of metrics, once split by \n
            // runtime.dotnet.threads.contention_time:0.4899|g|#lang:.NET,lang_interpreter:.NET,lang_version:7.0.9,tracer_version:2.38.0.0,runtime-id:b23d3d95-fefa-451f-8286-f6f5ad4aeb27,service:samples._runtimemetrics,env:integration_tests,version:1.0.0
            // runtime.dotnet.threads.contention_count:1|c|#lang:.NET,lang_interpreter:.NET,lang_version:7.0.9,tracer_version:2.38.0.0,runtime-id:b23d3d95-fefa-451f-8286-f6f5ad4aeb27,service:samples._runtimemetrics,env:integration_tests,version:1.0.0
            // runtime.dotnet.threads.contention_time:0|g|#lang:.NET,lang_interpreter:.NET,lang_version:7.0.9,tracer_version:2.38.0.0,runtime-id:b23d3d95-fefa-451f-8286-f6f5ad4aeb27,service:samples._runtimemetrics,env:integration_tests,version:1.0.0
            var metrics = requests.SelectMany(x => x.Split('\n')).ToList();

            // We don't expect any "internal" metrics
            metrics.Should().NotContain(x => x.StartsWith("datadog.dogstatsd.client."));

            // Assert tags
            metrics
               .Should()
               .OnlyContain(s => Regex.Matches(s, @"\bservice:samples\._runtimemetrics").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\benv:integration_tests").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\bversion:1\.0\.0").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\benv:").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\bversion:").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\bservice:").Count == 1)
               .And.OnlyContain(s => Regex.Matches(s, @"\bsome:value").Count == 1);

            // Check if .NET Framework or .NET Core 3.1+
            if (!EnvironmentHelper.IsCoreClr()
             || (Environment.Version.Major == 3 && Environment.Version.Minor == 1)
             || Environment.Version.Major >= 5)
            {
                var contentionRequestsCount = metrics.Count(r => r.StartsWith("runtime.dotnet.threads.contention_count"));

                Assert.True(contentionRequestsCount > 0, "No contention metrics received. Metrics received: " + string.Join("\n", requests));
            }

// using #if so it's a different test to the one we use in RuntimeMetricsWriter
#if NETFRAMEWORK || NETCOREAPP3_1_OR_GREATER
            var runtimeIsBuggy = false;
#else
            // https://github.com/dotnet/runtime/issues/23284
            var runtimeIsBuggy = !EnvironmentTools.IsWindows();
#endif
            if (runtimeIsBuggy)
            {
                requests.Should().NotContain(s => s.Contains(MetricsNames.CommittedMemory));
            }
            else
            {
                // these values shouldn't stay the same
                var memoryRequests = requests
                                    .Where(r => r.Contains(MetricsNames.CommittedMemory))
                                    .Select(
                                         r =>
                                         {
                                             _output.WriteLine($"Parsing metrics from {r}");
                                             // parse to find the memory
                                             var startIndex = r.IndexOf(MetricsNames.CommittedMemory, StringComparison.Ordinal);
                                             var separator = r.IndexOf(':', startIndex + 1);
                                             var endIndex = r.IndexOf('|', separator + 1);
                                             var name = r.Substring(startIndex, separator - startIndex);
                                             name.Should().Be(MetricsNames.CommittedMemory);
                                             return long.Parse(r.Substring(separator + 1, endIndex - separator - 1));
                                         })
                                    .ToList();

                if (memoryRequests.Count >= 2)
                {
                    // skip the case where we only get one metric for some reason
                    // Don't require completely distinct to reduce flake
                    memoryRequests.Distinct().Should().NotHaveCount(1);
                }
            }

            Assert.Empty(agent.Exceptions);
            VerifyInstrumentation(processResult.Process);
        }
    }
}
