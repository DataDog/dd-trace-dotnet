// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
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
            TracerInternal.Instance.Should().Be(tracerOne);
            TracerInternal.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(tracerTwo);
            TracerInternal.Instance.Should().Be(tracerTwo);
            TracerInternal.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            TracerInternal.Instance.Should().BeNull();
        }

        [Fact]
        public async Task LockedTracerInstanceSwap()
        {
            await using var tracerOne = TracerHelper.CreateWithFakeAgent();
            var tracerTwo = new LockedTracer();

            TracerRestorerAttribute.SetTracer(tracerOne);
            TracerInternal.Instance.Should().Be(tracerOne);
            TracerInternal.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            TracerInternal.Instance.Should().BeNull();

            // Set the locked tracer
            TracerRestorerAttribute.SetTracer(tracerTwo);
            TracerInternal.Instance.Should().Be(tracerTwo);
            TracerInternal.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            // We test the locked tracer cannot be replaced.
#pragma warning disable CS0618 // Setter isn't actually obsolete, just should be internal
            Assert.Throws<InvalidOperationException>(() => TracerInternal.Instance = tracerOne);

            Assert.Throws<ArgumentNullException>(() => TracerInternal.Instance = null);

            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, TracerManagerFactory.Instance));
            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, new CITracerManagerFactory(CIVisibility.Settings, NullDiscoveryService.Instance, false)));
        }

        [Fact]
        public void ReplacingGlobalTracerManagerMidTraceWritesTheTrace()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(null, agentPort))
            {
                var oldSettings = new TracerSettingsInternal
                {
                    Exporter = new ExporterSettingsInternal()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                    StartupDiagnosticLogEnabled = false,
                    GlobalTags = new Dictionary<string, string> { { "test-tag", "original-value" } },
                };
                TracerInternal.Configure(oldSettings);

                var scope = TracerInternal.Instance.StartActive("Test span");
                (scope.Span as Span).IsRootSpan.Should().BeTrue();

                var newSettings = new TracerSettingsInternal
                {
                    Exporter = new ExporterSettingsInternal()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                    StartupDiagnosticLogEnabled = false,
                    GlobalTags = new Dictionary<string, string> { { "test-tag", "new-value" } },
                };

                TracerInternal.Configure(newSettings);

                scope.Dispose();

                var spans = agent.WaitForSpans(count: 1);
                var received = spans.Should().ContainSingle().Subject;
                received.Name.Should().Be("Test span");
                received.Tags.Should().Contain("test-tag", "original-value");
            }
        }

        private class LockedTracer : TracerInternal
        {
            internal LockedTracer()
                : base(new LockedTracerManager())
            {
            }
        }

        private class LockedTracerManager : TracerManager, ILockedTracer
        {
            public LockedTracerManager()
                : base(new ImmutableTracerSettingsInternal(new TracerSettingsInternal()), null, null, null, null, null, null, null, null, null, null, null, null, Mock.Of<IRemoteConfigurationManager>(), Mock.Of<IDynamicConfigurationManager>(), Mock.Of<ITracerFlareManager>())
            {
            }
        }
    }
}
