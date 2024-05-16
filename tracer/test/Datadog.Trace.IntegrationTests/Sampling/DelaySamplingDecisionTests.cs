// <copyright file="DelaySamplingDecisionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Sampling;

public class DelaySamplingDecisionTests
{
    [Fact]
    public void SamplingDecisionIsNotMadeUntilLastSpanEnds()
    {
        var settings = new TracerSettings();
        var agentWriter = new Mock<IAgentWriter>();
        var sampler = new Mock<ITraceSampler>();
        var scopeManager = new AsyncLocalScopeManager();
        var statsd = new NoOpStatsd();

        var tracer = new Tracer(settings, agentWriter.Object, sampler.Object, scopeManager, statsd);
        TraceContext traceContext;

        using (var scope1 = (Scope)tracer.StartActive("operation"))
        {
            traceContext = scope1.Span.Context.TraceContext;

            // sampling decision not taken when first span starts
            sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Never);
            traceContext.SamplingPriority.Should().BeNull();

            using ((Scope)tracer.StartActive("operation"))
            {
                // sampling decision still not taken when second span starts
                sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Never);
                traceContext.SamplingPriority.Should().BeNull();
            }

            // sampling decision still not taken when second span ends
            sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Never);
            traceContext.SamplingPriority.Should().BeNull();
        }

        // sampling decision IS taken after LAST span ends
        sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Once);
        traceContext.SamplingPriority.Should().NotBeNull();
    }

    [Fact]
    public void SamplingDecisionIsMadeWhenPropagating()
    {
        var settings = new TracerSettings();
        var agentWriter = new Mock<IAgentWriter>();
        var sampler = new Mock<ITraceSampler>();
        var scopeManager = new AsyncLocalScopeManager();
        var statsd = new NoOpStatsd();

        var tracer = new Tracer(settings, agentWriter.Object, sampler.Object, scopeManager, statsd);
        TraceContext traceContext;
        int samplingPriority;

        using (var scope1 = (Scope)tracer.StartActive("operation"))
        {
            traceContext = scope1.Span.Context.TraceContext;

            // sampling decision not taken when first span starts
            sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Never);
            traceContext.SamplingPriority.Should().BeNull();

            var headers = new NameValueCollection();
            SpanContextPropagator.Instance.Inject(scope1.Span.Context, headers.Wrap());

            // sampling decision IS taken before propagating
            sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Once);
            traceContext.SamplingPriority.Should().NotBeNull();
            headers["x-datadog-sampling-priority"].Should().Be(SamplingPriorityValues.ToString(traceContext.SamplingPriority));

            samplingPriority = traceContext.SamplingPriority!.Value;
        }

        // sampling decision hasn't changed and is NOT taken again when first span ends
        sampler.Verify(s => s.MakeSamplingDecision(It.IsAny<Span>()), Times.Once);
        traceContext.SamplingPriority.Should().Be(samplingPriority);
    }
}
