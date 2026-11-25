// <copyright file="HttpRequestMetricTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.IO;
using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using FluentAssertions;
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

        [TestAppFact(TestApplication.ParallelCountSites, MinimumFramework = TestFramework.Net7)]
        public void CheckAllMetrics(TestAppContext app)
        {
            var runner = app.CreateRunner(_output, commandLine: All);
            runner.Environment.EnableOnly(ProfilerFeature.Network, ProfilerFeature.ForceHttpSampling);

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasMetrics = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasMetrics = MetricHelper.GetMetrics(ctx.Value.Request);
            };

            runner.Run(agent);

            // uncomment to debug multipart http request
            // Thread.Sleep(10000000);

            hasMetrics.Should().BeTrue();
            ValidateAllMetrics(runner.Environment.PprofDir);
        }

        private static void ValidateAllMetrics(string directory)
        {
            var metricsFiles = Directory.GetFiles(directory, "metrics_*.json");
            metricsFiles.Length.Should().BeGreaterThan(0);
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
                handshakeDurationSum.Should().BeGreaterThanOrEqualTo(0);
                handshakeDurationMean.Should().BeGreaterThanOrEqualTo(0);
                handshakeDurationMax.Should().BeGreaterThanOrEqualTo(0);
                handshakeDurationMean.Should().BeLessThanOrEqualTo(handshakeDurationMax);
                handshakeDurationMax.Should().BeLessThanOrEqualTo(handshakeDurationSum);

                dnsDurationSum.Should().BeGreaterThanOrEqualTo(0);
                dnsDurationMean.Should().BeGreaterThanOrEqualTo(0);
                dnsDurationMax.Should().BeGreaterThanOrEqualTo(0);
                dnsDurationMean.Should().BeLessThanOrEqualTo(dnsDurationMax);
                dnsDurationMax.Should().BeLessThanOrEqualTo(dnsDurationSum);

                requestDurationSum.Should().BeGreaterThanOrEqualTo(0);
                requestDurationMean.Should().BeGreaterThanOrEqualTo(0);
                requestDurationMax.Should().BeGreaterThanOrEqualTo(0);
                requestDurationMean.Should().BeLessThanOrEqualTo(requestDurationMax);
                requestDurationMax.Should().BeLessThanOrEqualTo(requestDurationSum);

                waitDurationSum.Should().BeGreaterThanOrEqualTo(0);
                waitDurationMean.Should().BeGreaterThanOrEqualTo(0);
                waitDurationMax.Should().BeGreaterThanOrEqualTo(0);
                waitDurationMean.Should().BeLessThanOrEqualTo(waitDurationMax);
                waitDurationMax.Should().BeLessThanOrEqualTo(waitDurationSum);

                responseDurationSum.Should().BeGreaterThanOrEqualTo(0);
                responseDurationMean.Should().BeGreaterThanOrEqualTo(0);
                responseDurationMax.Should().BeGreaterThanOrEqualTo(0);
                responseDurationMean.Should().BeLessThanOrEqualTo(responseDurationMax);
                responseDurationMax.Should().BeLessThanOrEqualTo(responseDurationSum);

                requestAllCount.Should().BeGreaterThanOrEqualTo(0);
                requestFailedCount.Should().BeGreaterThanOrEqualTo(0);
                requestRedirectCount.Should().BeGreaterThanOrEqualTo(0);
                requestFailedCount.Should().BeLessThanOrEqualTo(requestAllCount);
                requestRedirectCount.Should().BeLessThanOrEqualTo(requestAllCount);
            }
        }
    }
}
