// <copyright file="RuntimeMetricsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
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

#if NET6_0_OR_GREATER
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task DiagnosticsMetricsApiSubmitsMetrics()
        {
            SetEnvironmentVariable(ConfigurationKeys.RuntimeMetricsDiagnosticsMetricsApiEnabled, "1");
            EnvironmentHelper.EnableDefaultTransport();
            await RunTest();
        }
#endif

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
        [Trait("Category", "LinuxUnsupported")]
        [Trait("RunOnWindows", "True")]
        [Flaky("Named pipes is flaky", maxRetries: 3)]
        public async Task NamedPipesSubmitsMetrics()
        {
            if (!EnvironmentTools.IsWindows())
            {
                throw new SkipException("WindowsNamedPipe transport is only supported on Windows");
            }

            EnvironmentHelper.EnableWindowsNamedPipes();
            await RunTest();
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task ProcessTagsIncludedInMetrics_WhenEnabled()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            EnvironmentHelper.EnableDefaultTransport();
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");
            SetEnvironmentVariable("DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED", "1");
            SetEnvironmentVariable("DD_SERVICE", "Samples.RuntimeMetrics");

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using var processResult = await RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            Assert.True(requests.Count > 0, "No metrics received");

            var metrics = requests.SelectMany(x => x.Split('\n')).ToList();

            // Verify process tags are present in the metrics
            // Process tags include: entrypoint.basedir, entrypoint.workdir, and optionally entrypoint.name
            metrics
               .Should()
               .OnlyContain(s => s.Contains("entrypoint.basedir:"), "entrypoint.basedir process tag should be present")
               .And.OnlyContain(s => s.Contains("entrypoint.workdir:"), "entrypoint.workdir process tag should be present");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task ProcessTagsNotIncludedInMetrics_WhenDisabled()
        {
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);
            EnvironmentHelper.EnableDefaultTransport();
            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");
            SetEnvironmentVariable("DD_EXPERIMENTAL_PROPAGATE_PROCESS_TAGS_ENABLED", "0");
            SetEnvironmentVariable("DD_SERVICE", "Samples.RuntimeMetrics");

            using var agent = EnvironmentHelper.GetMockAgent(useStatsD: true);
            using var processResult = await RunSampleAndWaitForExit(agent);
            var requests = agent.StatsdRequests;

            Assert.True(requests.Count > 0, "No metrics received");

            var metrics = requests.SelectMany(x => x.Split('\n')).ToList();

            // Verify process tags are NOT present in the metrics when disabled
            metrics
               .Should()
               .NotContain(s => s.Contains("entrypoint.basedir:"), "entrypoint.basedir should not be present when process tags are disabled")
               .And.NotContain(s => s.Contains("entrypoint.workdir:"), "entrypoint.workdir should not be present when process tags are disabled")
               .And.NotContain(s => s.Contains("entrypoint.name:"), "entrypoint.name should not be present when process tags are disabled");
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Validates that all 19 OTel-native .NET runtime metrics are exported via OTLP
        /// with correct names, types, tags, and structure via snapshot testing.
        /// Follows the same scrub-and-snapshot pattern as OpenTelemetrySdkTests.SubmitsOtlpMetrics.
        /// Tag requirements driven by: https://github.com/DataDog/semantic-core/blob/main/sor/domains/metrics/integrations/dotnet/_equivalence/otel_dd.yaml
        /// Instrument surface ref: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime
        /// </summary>
        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("RequiresDockerDependency", "true")]
        public async Task OtlpRuntimeMetricsSubmitted()
        {
            var testAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "localhost";
            const int otlpPort = 4318;

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                await httpClient.GetAsync($"http://{testAgentHost}:{otlpPort}/test/session/clear");
            }

            SetEnvironmentVariable("DD_RUNTIME_METRICS_ENABLED", "1");
            SetEnvironmentVariable("DD_METRICS_OTEL_ENABLED", "1");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", $"http://{testAgentHost}:{otlpPort}");
            SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent))
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var metricsResponse = await httpClient.GetAsync($"http://{testAgentHost}:{otlpPort}/test/session/metrics");
                metricsResponse.EnsureSuccessStatusCode();

                var metricsJson = await metricsResponse.Content.ReadAsStringAsync();
                var metricsData = JToken.Parse(metricsJson);

                metricsData.Should().NotBeNullOrEmpty();

                // --- Extract and validate metric names ---
                var metricNames = metricsData
                    .SelectTokens("$..metrics[*].name")
                    .Select(t => t.ToString())
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                _output.WriteLine("Received OTLP metric names:\n  " + string.Join("\n  ", metricNames));

                // All 19 OTel-native System.Runtime instruments must be present
                var expectedMetrics = new[]
                {
                    "dotnet.assembly.count",
                    "dotnet.exceptions",
                    "dotnet.gc.collections",
                    "dotnet.gc.heap.total_allocated",
                    "dotnet.gc.last_collection.heap.fragmentation.size",
                    "dotnet.gc.last_collection.heap.size",
                    "dotnet.gc.last_collection.memory.committed_size",
                    "dotnet.gc.pause.time",
                    "dotnet.jit.compilation.time",
                    "dotnet.jit.compiled_il.size",
                    "dotnet.jit.compiled_methods",
                    "dotnet.monitor.lock_contentions",
                    "dotnet.process.cpu.count",
                    "dotnet.process.cpu.time",
                    "dotnet.process.memory.working_set",
                    "dotnet.thread_pool.queue.length",
                    "dotnet.thread_pool.thread.count",
                    "dotnet.thread_pool.work_item.count",
                    "dotnet.timer.count",
                };

                foreach (var expected in expectedMetrics)
                {
                    metricNames.Should().Contain(expected, $"expected OTLP runtime metric '{expected}' to be present");
                }

                // --- Scrub volatile fields and build snapshot ---
                // Zero out timestamps (vary per run)
                foreach (var dataPoint in metricsData.SelectTokens("$..data_points[*]"))
                {
                    dataPoint["start_time_unix_nano"] = "0";
                    dataPoint["time_unix_nano"] = "0";

                    // Zero out volatile metric values (memory sizes, cpu times, counts change per run)
                    if (dataPoint["as_int"] is not null)
                    {
                        dataPoint["as_int"] = "0";
                    }

                    if (dataPoint["as_double"] is not null)
                    {
                        dataPoint["as_double"] = 0.0;
                    }
                }

                // Scrub resource attributes that vary (sdk version, runtime-id, etc.)
                foreach (var attribute in metricsData.SelectTokens("$..resource.attributes[?(@.key == 'telemetry.sdk.version')]"))
                {
                    attribute["value"]!["string_value"] = "sdk-version";
                }

                // Keep only System.Runtime metrics, deduplicate by name to avoid
                // multiple export intervals making the snapshot non-deterministic.
                // For metrics with tagged data points (e.g. gc.heap.generation=gen0/gen1/gen2),
                // merge all data points and sort by tag values for stable ordering.
                var metricsByName = new Dictionary<string, JObject>();
                foreach (var metric in metricsData.SelectTokens("$..metrics[*]"))
                {
                    var name = metric["name"]?.ToString();
                    if (name is null || !name.StartsWith("dotnet."))
                    {
                        continue;
                    }

                    if (!metricsByName.ContainsKey(name))
                    {
                        metricsByName[name] = (JObject)metric.DeepClone();
                    }
                    else
                    {
                        // Merge data points from duplicate metric entries (multiple export intervals)
                        var existing = metricsByName[name];
                        var existingDps = existing.SelectTokens("$..data_points").OfType<JArray>().FirstOrDefault();
                        var newDps = metric.SelectTokens("$..data_points").OfType<JArray>().FirstOrDefault();
                        if (existingDps is not null && newDps is not null)
                        {
                            foreach (var dp in newDps)
                            {
                                existingDps.Add(dp.DeepClone());
                            }
                        }
                    }
                }

                // For each metric, deduplicate data points by tag signature and keep one per unique tag combo.
                // Sort data points by their tag values for deterministic ordering.
                foreach (var metric in metricsByName.Values)
                {
                    var dpArrays = metric.SelectTokens("$..data_points").OfType<JArray>().ToList();
                    foreach (var dpArray in dpArrays)
                    {
                        var seenTags = new HashSet<string>();
                        var dedupedDps = new JArray();
                        foreach (var dp in dpArray)
                        {
                            var tagSig = string.Join(",", dp.SelectTokens("$.attributes[*]")
                                .Select(a => $"{a["key"]}={a["value"]?["string_value"]}")
                                .OrderBy(t => t));
                            if (seenTags.Add(tagSig))
                            {
                                dedupedDps.Add(dp.DeepClone());
                            }
                        }

                        // Sort data points by their tag signature
                        var sortedDps = new JArray(dedupedDps.OrderBy(dp =>
                            string.Join(",", dp.SelectTokens("$.attributes[*]")
                                .Select(a => $"{a["key"]}={a["value"]?["string_value"]}")
                                .OrderBy(t => t))));

                        dpArray.Clear();
                        foreach (var dp in sortedDps)
                        {
                            dpArray.Add(dp);
                        }
                    }
                }

                var sorted = new JArray(metricsByName.Values.OrderBy(m => m["name"]?.ToString()));

                // The snapshot captures: metric names, types (sum/gauge), temporality,
                // is_monotonic, units, and tag keys+values (cpu.mode, gc.heap.generation).
                // Volatile data (timestamps, actual values) are zeroed out.
                var formattedJson = sorted.ToString(Formatting.Indented);

                var verifySettings = VerifyHelper.GetSpanVerifierSettings();
                VerifyHelper.InitializeGlobalSettings();
#if NET9_0_OR_GREATER
                const string tfm = "net9";
#else
                const string tfm = "net6";
#endif
                var fileName = $"RuntimeMetricsTests.OtlpRuntimeMetricsSubmitted_{tfm}";
                verifySettings.UseFileName(fileName);
                verifySettings.DisableRequireUniquePrefix();

                await Verifier.Verify(formattedJson, verifySettings);
            }
        }
#endif

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
