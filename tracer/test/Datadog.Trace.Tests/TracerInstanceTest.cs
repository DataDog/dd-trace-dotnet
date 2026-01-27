// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class TracerInstanceTest
    {
        [Fact]
        public async Task NormalTracerInstanceSwap()
        {
            await using var tracerOne = TracerHelper.CreateWithFakeAgent();
            await using var tracerTwo = TracerHelper.CreateWithFakeAgent();

            TracerRestorerAttribute.SetTracer(tracerOne);
            Tracer.Instance.Should().Be(tracerOne);
            Tracer.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(tracerTwo);
            Tracer.Instance.Should().Be(tracerTwo);
            Tracer.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            Tracer.Instance.Should().BeNull();
        }

        [Fact]
        public async Task LockedTracerInstanceSwap()
        {
            await using var tracerOne = TracerHelper.CreateWithFakeAgent();
            var tracerTwo = new LockedTracer();

            TracerRestorerAttribute.SetTracer(tracerOne);
            Tracer.Instance.Should().Be(tracerOne);
            Tracer.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            Tracer.Instance.Should().BeNull();

            // Set the locked tracer
            TracerRestorerAttribute.SetTracer(tracerTwo);
            Tracer.Instance.Should().Be(tracerTwo);
            Tracer.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, TracerManagerFactory.Instance));
            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, new TestOptimizationTracerManagerFactory(TestOptimization.Instance.Settings, TestOptimization.Instance.TracerManagement!, false)));
        }

        [Fact]
        public async Task ReplacingGlobalTracerManagerMidTraceWritesTheTrace()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(null, agentPort))
            {
                var oldSettings = TracerSettings.Create(
                    new()
                    {
                        { ConfigurationKeys.AgentUri, new Uri($"http://127.0.0.1:{agent.Port}") },
                        { ConfigurationKeys.TracerMetricsEnabled, false },
                        { ConfigurationKeys.StartupDiagnosticLogEnabled, false },
                        { ConfigurationKeys.GlobalTags, "test-tag:original-value" },
                    });
                Tracer.Configure(oldSettings);

                var scope = Tracer.Instance.StartActive("Test span");
                (scope.Span as Span).IsRootSpan.Should().BeTrue();

                var newSettings = TracerSettings.Create(
                    new()
                    {
                        { ConfigurationKeys.AgentUri, new Uri($"http://127.0.0.1:{agent.Port}") },
                        { ConfigurationKeys.TracerMetricsEnabled, false },
                        { ConfigurationKeys.StartupDiagnosticLogEnabled, false },
                        { ConfigurationKeys.GlobalTags, "test-tag:new-value" },
                    });

                Tracer.Configure(newSettings);

                scope.Dispose();

                var spans = await agent.WaitForSpansAsync(count: 1);
                var received = spans.Should().ContainSingle().Subject;
                received.Name.Should().Be("Test span");
                received.Tags.Should().Contain("test-tag", "original-value");
            }
        }

        private class LockedTracer : Tracer
        {
            internal LockedTracer()
                : base(new LockedTracerManager())
            {
            }
        }

        private class LockedTracerManager : TracerManager, ILockedTracer
        {
            public LockedTracerManager()
                : base(new TracerSettings(), null, null, null, null, null, null, null, null, null, null, null, Mock.Of<IRemoteConfigurationManager>(), Mock.Of<IDynamicConfigurationManager>(), Mock.Of<ITracerFlareManager>(), Mock.Of<ISpanEventsManager>(), null)
            {
            }
        }
    }
}
