// <copyright file="HeapSnapshotTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Net;
using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests
{
    public class HeapSnapshotTest
    {
        private readonly ITestOutputHelper _output;

        public HeapSnapshotTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01", new[] { "net6.0", "net8.0", "net10.0" })]
        public void CheckHeapSnapshot(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: "--scenario 13");
            //runner.Environment.SetVariable(EnvironmentVariables.ManagedActivationEnabled, "0");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.HeapSnapshotMemoryPressureThreshold, "0");

            using var agent = MockDatadogAgent.CreateHttpAgent(runner.XUnitLogger);
            bool hasHeapSnapshot = false;
            agent.ProfilerRequestReceived += (object sender, EventArgs<HttpListenerContext> ctx) =>
            {
                hasHeapSnapshot = HeapSnapshotHelper.HasHeapSnapshot(ctx.Value.Request);
            };

            runner.Run(agent);

            // uncomment to debug multipart http request
            // Thread.Sleep(10000000);

            Assert.True(hasHeapSnapshot);
        }
    }
}
