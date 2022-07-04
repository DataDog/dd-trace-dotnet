// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class TracerInstanceTest
    {
        [Fact]
        public void NormalTracerInstanceSwap()
        {
            var tracerOne = TracerHelper.Create();
            var tracerTwo = TracerHelper.Create();

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
        public void LockedTracerInstanceSwap()
        {
            var tracerOne = TracerHelper.Create();
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

            // We test the locked tracer cannot be replaced.
#pragma warning disable CS0618 // Setter isn't actually obsolete, just should be internal
            Assert.Throws<InvalidOperationException>(() => Tracer.Instance = tracerOne);

            Assert.Throws<ArgumentNullException>(() => Tracer.Instance = null);

            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, TracerManagerFactory.Instance));
            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, new CITracerManagerFactory(CIVisibility.Settings)));
        }

        [Fact]
        public void ReplacingGlobalTracerManagerMidTraceWritesTheTrace()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(agentPort))
            {
                var oldSettings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                    StartupDiagnosticLogEnabled = false,
                    GlobalTags = new Dictionary<string, string> { { "test-tag", "original-value" } },
                };
                Tracer.Configure(oldSettings);

                var span = Tracer.Instance.StartActive("Test span");

                var newSettings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                    StartupDiagnosticLogEnabled = false,
                    GlobalTags = new Dictionary<string, string> { { "test-tag", "new-value" } },
                };

                Tracer.Configure(newSettings);

                span.Dispose();

                var spans = agent.WaitForSpans(count: 1);
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
                : base(null, null, null, null, null, null, null, null, null)
            {
            }
        }
    }
}
