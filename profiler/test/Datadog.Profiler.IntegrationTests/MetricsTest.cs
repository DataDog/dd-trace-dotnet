// <copyright file="MetricsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class MetricsTest
    {
        private readonly ITestOutputHelper _output;

        public MetricsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net7.0", "net8.0" })]
        public void CheckMetrics(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 10 --threads 20");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.GarbageCollectionProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasMetrics = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasMetrics = MetricHelper.GetMetrics(ctx.Value.Request);
            };

            runner.Run(agent);

            // uncomment to debug multipart http request
            // Thread.Sleep(10000000);

            Assert.True(hasMetrics);
            ValidateMetrics(runner.Environment.PprofDir);
        }

        private static void ValidateMetrics(string directory)
        {
            var metricsFiles = Directory.GetFiles(directory, "metrics_*.json");
            Assert.True(metricsFiles.Length > 0);
            foreach (var metricsFile in metricsFiles)
            {
                // get the metrics from the local json file
                var metrics = MetricHelper.GetMetrics(metricsFile);
                double threadCount = -1;
                double threadCountLow = -1;
                double threadCountHigh = -1;

                foreach (var metric in metrics)
                {
                    if (metric.Item1 == "dotnet_managed_threads")
                    {
                        threadCount = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_managed_threads_low")
                    {
                        threadCountLow = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_managed_threads_high")
                    {
                        threadCountHigh = metric.Item2;
                    }
                }

                Assert.True(threadCount > 0);
                Assert.True(threadCountLow > 0);
                Assert.True(threadCountHigh > 0);

                Assert.True(threadCountLow <= threadCount);
                Assert.True(threadCount <= threadCountHigh);
            }
        }
    }
}
