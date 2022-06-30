// <copyright file="OpenTracingSendTracesToAgent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.OpenTracing.IntegrationTests
{
    public class OpenTracingSendTracesToAgent
    {
        [SkippableFact]
        public void MinimalSpan()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(agentPort))
            {
                var settings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                };

                var innerTracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
                var tracer = new OpenTracingTracer(innerTracer);

                var span = (OpenTracingSpan)tracer.BuildSpan("Operation")
                                                   .Start();
                span.Finish();

                var spans = agent.WaitForSpans(1);

                var receivedSpan = spans.Should().ContainSingle().Subject;
                CompareSpans(receivedSpan, span);
            }
        }

        [SkippableFact]
        public void CustomServiceName()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(agentPort))
            {
                var settings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                };

                var innerTracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
                var tracer = new OpenTracingTracer(innerTracer);

                var span = (OpenTracingSpan)tracer.BuildSpan("Operation")
                                                  .WithTag(DatadogTags.ResourceName, "This is a resource")
                                                  .WithTag(DatadogTags.ServiceName, "MyService")
                                                  .Start();
                span.Finish();

                var spans = agent.WaitForSpans(1);

                var receivedSpan = spans.Should().ContainSingle().Subject;
                CompareSpans(receivedSpan, span);
            }
        }

        [SkippableFact]
        public void Utf8Everywhere()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(agentPort))
            {
                var settings = new TracerSettings
                {
                    Exporter = new ExporterSettings()
                    {
                        AgentUri = new Uri($"http://127.0.0.1:{agent.Port}"),
                    },
                    TracerMetricsEnabled = false,
                };

                var innerTracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);
                var tracer = new OpenTracingTracer(innerTracer);

                var span = (OpenTracingSpan)tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                                                  .WithTag(DatadogTags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                                                  .WithTag(DatadogTags.ServiceName, "На берегу пустынных волн")
                                                  .WithTag("யாமறிந்த", "ნუთუ კვლა")
                                                  .Start();
                span.Finish();

                var spans = agent.WaitForSpans(1);

                var receivedSpan = spans.Should().ContainSingle().Subject;
                CompareSpans(receivedSpan, span);
            }
        }

        private static void CompareSpans(MockSpan receivedSpan, OpenTracingSpan openTracingSpan)
        {
            var span = (Span)openTracingSpan.Span;
            receivedSpan.Should().BeEquivalentTo(new
            {
                TraceId = span.TraceId,
                SpanId = span.SpanId,
                Name = span.OperationName,
                Resource = span.ResourceName,
                Service = span.ServiceName,
                Type = span.Type,
                Tags = span.Tags,
            });
        }
    }
}
