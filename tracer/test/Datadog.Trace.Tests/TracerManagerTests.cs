// <copyright file="TracerManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TracerManagerTests
    {
        [Fact]
        public async Task ReplacingATracerManagerMidTrace()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                var settings = new TracerSettings
                {
                    AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    TracerMetricsEnabled = false,
                    StartupDiagnosticLogEnabled = false,
                };

                var tracer1 = TracerHelper.Create(settings);

                var span = tracer1.StartActive("Test span");

                // create a new tracer, and clean up the old one.
                // similar to what happens when we call ReplaceTracerManager
                var oldManager = tracer1.TracerManager;
                var tracer2 = TracerHelper.Create(settings, scopeManager: oldManager.ScopeManager);
                await TracerManager.CleanUpOldTracerManager(oldManager, newManager: tracer2.TracerManager);

                span.Dispose();

                var spans = agent.WaitForSpans(count: 1);
            }
        }
    }
}
