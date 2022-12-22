// <copyright file="MetricsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;
using System.Threading;
using Datadog.Profiler.IntegrationTests.Helpers;
using Datadog.Profiler.SmokeTests;
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

        [TestAppFact("Samples.Computer01", new[] { "net6.0" })]
        public void CheckMetrics(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 10 --threads 20");
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.ContentionProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            bool hasMetrics = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasMetrics = GetMetrics(ctx.Value.Request);
            };

            runner.Run(agent);

            // uncomment to debug multipart http request
            // Thread.Sleep(10000000);

            Assert.True(hasMetrics);
        }

        private bool GetMetrics(HttpListenerRequest request)
        {
            if (!request.ContentType.StartsWith("multipart/form-data"))
            {
                return false;
            }

            var mpReader = new MultiPartReader(request);
            if (!mpReader.Parse())
            {
                return false;
            }

            var files = mpReader.Files;
            var metricsFileInfo = files.FirstOrDefault(f => f.FileName == "metrics.json");
            if (metricsFileInfo == null)
            {
                return false;
            }

            // TODO: when the file will be generated the right way, parse the json content to ensure that at least 1 metrics is sent
            var metricsFileContent = mpReader.GetStringFile(metricsFileInfo.BytesPos, metricsFileInfo.BytesSize);
            // Today, the content is not correct and with a binary format

            return true;
        }
    }
}
