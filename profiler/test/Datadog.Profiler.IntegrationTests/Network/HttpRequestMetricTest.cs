// <copyright file="HttpRequestMetricTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Network
{
    public class HttpRequestMetricTest
    {
        private const string All = "--iterations 5 --scenario 7";

        private readonly ITestOutputHelper _output;

        public HttpRequestMetricTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.ParallelCountSites", new[] { "net7.0", "net8.0", "net9.0" })]
        public void CheckAllMetrics(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: All);
            EnvironmentHelper.DisableDefaultProfilers(runner);
            runner.Environment.SetVariable(EnvironmentVariables.HttpProfilingEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.ForceHttpSamplingEnabled, "1");

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
            ValidateAllMetrics(runner.Environment.PprofDir);
        }

        private static void ValidateAllMetrics(string directory)
        {
            var metricsFiles = Directory.GetFiles(directory, "metrics_*.json");
            Assert.True(metricsFiles.Length > 0);
            foreach (var metricsFile in metricsFiles)
            {
                // get the metrics from the local json file
                var metrics = MetricHelper.GetMetrics(metricsFile);
                double handshakeDurationSum = -1;
                double handshakeDurationMean = -1;
                double handshakeDurationMax = -1;
                double dnsDurationSum = -1;
                double dnsDurationMean = -1;
                double dnsDurationMax = -1;
                double requestDurationSum = -1;
                double requestDurationMean = -1;
                double requestDurationMax = -1;
                double requestAllCount = -1;
                double requestFailedCount = -1;
                double requestRedirectCount = -1;
                double waitDurationSum = -1;
                double waitDurationMean = -1;
                double waitDurationMax = -1;
                double responseDurationSum = -1;
                double responseDurationMean = -1;
                double responseDurationMax = -1;

                foreach (var metric in metrics)
                {
                    if (metric.Item1 == "dotnet_request_handshake_duration_sum")
                    {
                        handshakeDurationSum = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_handshake_duration_mean")
                    {
                        handshakeDurationMean = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_handshake_duration_max")
                    {
                        handshakeDurationMax = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_dns_duration_sum")
                    {
                        dnsDurationSum = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_dns_duration_mean")
                    {
                        dnsDurationMean = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_dns_duration_max")
                    {
                        dnsDurationMax = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_duration_sum")
                    {
                        requestDurationSum = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_duration_mean")
                    {
                        requestDurationMean = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_duration_max")
                    {
                        requestDurationMax = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_all_count")
                    {
                        requestAllCount = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_failed_count")
                    {
                        requestFailedCount = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_redirect_count")
                    {
                        requestRedirectCount = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_wait_duration_sum")
                    {
                        waitDurationSum = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_wait_duration_mean")
                    {
                        waitDurationMean = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_wait_duration_max")
                    {
                        waitDurationMax = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_response_duration_sum")
                    {
                        responseDurationSum = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_response_duration_mean")
                    {
                        responseDurationMean = metric.Item2;
                    }
                    else if (metric.Item1 == "dotnet_request_response_duration_max")
                    {
                        responseDurationMax = metric.Item2;
                    }
                }

                // these checks could be flacky so just check that the values are not negative (i.e. the events have been received but with a 0 duration)
                Assert.True(handshakeDurationSum >= 0);
                Assert.True(handshakeDurationMean >= 0);
                Assert.True(handshakeDurationMax >= 0);
                Assert.True(handshakeDurationMean <= handshakeDurationMax);
                Assert.True(handshakeDurationMax <= handshakeDurationSum);

                Assert.True(dnsDurationSum >= 0);
                Assert.True(dnsDurationMean >= 0);
                Assert.True(dnsDurationMax >= 0);
                Assert.True(dnsDurationMean <= dnsDurationMax);
                Assert.True(dnsDurationMax <= dnsDurationSum);

                Assert.True(requestDurationSum >= 0);
                Assert.True(requestDurationMean >= 0);
                Assert.True(requestDurationMax >= 0);
                Assert.True(requestDurationMean <= requestDurationMax);
                Assert.True(requestDurationMax <= requestDurationSum);

                Assert.True(waitDurationSum >= 0);
                Assert.True(waitDurationMean >= 0);
                Assert.True(waitDurationMax >= 0);
                Assert.True(waitDurationMean <= waitDurationMax);
                Assert.True(waitDurationMax <= waitDurationSum);

                Assert.True(responseDurationSum >= 0);
                Assert.True(responseDurationMean >= 0);
                Assert.True(responseDurationMax >= 0);
                Assert.True(responseDurationMean <= responseDurationMax);
                Assert.True(responseDurationMax <= responseDurationSum);

                Assert.True(requestAllCount >= 0);
                Assert.True(requestFailedCount >= 0);
                Assert.True(requestRedirectCount >= 0);
                Assert.True(requestFailedCount <= requestAllCount);
                Assert.True(requestRedirectCount <= requestAllCount);
            }
        }
    }
}
