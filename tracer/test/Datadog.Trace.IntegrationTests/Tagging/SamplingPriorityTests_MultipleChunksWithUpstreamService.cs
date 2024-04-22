// <copyright file="SamplingPriorityTests_MultipleChunksWithUpstreamService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

public class SamplingPriorityTests_MultipleChunksWithUpstreamService
{
    private const string SamplingPriorityName = "_sampling_priority_v1";
    private const int SamplingPriorityValue = 1;

    private readonly MockApi _testApi;
    private readonly Tracer _tracer;
    private AgentWriter _agentWriter;

    public SamplingPriorityTests_MultipleChunksWithUpstreamService()
    {
        _testApi = new MockApi();

        var settings = new TracerSettings();
        _agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null, automaticFlush: false);
        _tracer = new Tracer(settings, _agentWriter, sampler: null, scopeManager: null, statsd: null);
    }

    [Fact]
    public async Task SingleChunk()
    {
        var propagatedContext = new SpanContext(traceId: 1, spanId: 10);
        var settings = new SpanCreationSettings { Parent = propagatedContext };

        using (var scope1 = _tracer.StartActive("1", settings))
        {
            ((Span)scope1.Span).Context.TraceContext.SetSamplingPriority(SamplingPriorityValue);

            using (_ = _tracer.StartActive("1.1"))
            {
            }

            using (_ = _tracer.StartActive("1.2"))
            {
                using (_ = _tracer.StartActive("1.2.1"))
                {
                }
            }
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1, 1.2, 1.2.1, 1 ]

        traceChunks.Should().HaveCount(1);    // 1 trace chunk
        traceChunks[0].Should().HaveCount(4); // 4 spans

        // root span should have the sampling priority
        var mockSpan1 = traceChunks[0][3];
        mockSpan1.ParentId.Should().Be(propagatedContext.SpanId);
        mockSpan1.Metrics.Should().Contain(SamplingPriorityName, SamplingPriorityValue);

        // other spans should NOT have the sampling priority
        traceChunks[0]
           .Where(s => s.ParentId != propagatedContext.SpanId)
           .Should()
           .HaveCount(3)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == null);
    }

    [Fact]
    public async Task MultipleChunks_3_and_1_Spans()
    {
        ISpan span1;
        ISpan span11;
        ISpan span12;
        ISpan span121;

        var propagatedContext = new SpanContext(traceId: 1, spanId: 10);
        var settings = new SpanCreationSettings { Parent = propagatedContext };

        using (var scope1 = _tracer.StartActive("1", settings))
        {
            span1 = scope1.Span;

            var traceContext = ((Scope)scope1).Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValue);

            using (var scope11 = _tracer.StartActive("1.1"))
            {
                span11 = scope11.Span;
            }

            using (var scope12 = _tracer.StartActive("1.2"))
            {
                span12 = scope12.Span;

                using (var scope121 = _tracer.StartActive("1.2.1"))
                {
                    span121 = scope121.Span;
                }
            }

            // send the finished spans as one trace chunk
            traceContext.WriteClosedSpans();
        }

        // send the remaining spans as another trace chunk
        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1, 1.2, 1.2.1 ]
        // [ 1 ]

        traceChunks.Should().HaveCount(2);    // 2 trace chunks
        traceChunks[0].Should().HaveCount(3); // 3 spans
        traceChunks[1].Should().HaveCount(1); // 1 span

        // chunk 0, both orphan spans should have the sampling priority
        traceChunks[0]
           .Where(s => s.SpanId == span11.SpanId || s.SpanId == span12.SpanId)
           .Should()
           .HaveCount(2)
           .And.OnlyContain(s => s.ParentId == span1.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);

        // chunk 0, other spans should NOT have the sampling priority
        traceChunks[0]
           .Where(s => s.SpanId == span121.SpanId)
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == span12.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == null);

        // chunk 1, orphan span should have the sampling priority
        traceChunks[1]
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == propagatedContext.SpanId)
           .And.OnlyContain(s => s.SpanId == span1.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);
    }

    [Fact]
    public async Task MultipleChunks_2_and_2_Spans()
    {
        ISpan span1;
        ISpan span11;
        ISpan span12;
        ISpan span121;

        var propagatedContext = new SpanContext(traceId: 1, spanId: 10);
        var settings = new SpanCreationSettings { Parent = propagatedContext };

        using (var scope1 = _tracer.StartActive("1", settings))
        {
            span1 = scope1.Span;

            var traceContext = ((Scope)scope1).Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValue);

            using (var scope11 = _tracer.StartActive("1.1"))
            {
                span11 = scope11.Span;
            }

            using (var scope12 = _tracer.StartActive("1.2"))
            {
                span12 = scope12.Span;

                using (var scope121 = _tracer.StartActive("1.2.1"))
                {
                    span121 = scope121.Span;
                }

                // send the finished spans as one trace chunk
                traceContext.WriteClosedSpans();
            }
        }

        // send the remaining spans as another trace chunk
        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1, 1.2.1 ]
        // [ 1.2, 1 ]

        traceChunks.Should().HaveCount(2);    // 2 trace chunks
        traceChunks[0].Should().HaveCount(2); // 2 spans
        traceChunks[1].Should().HaveCount(2); // 2 spans

        // chunk 0, both spans should have the sampling priority
        traceChunks[0]
           .Should()
           .HaveCount(2)
           .And.OnlyContain(s => s.SpanId == span11.SpanId || s.SpanId == span121.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);

        // chunk 1, orphan span should have the sampling priority
        traceChunks[1]
           .Where(s => s.SpanId == span1.SpanId)
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == propagatedContext.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);

        // chunk 1, the other span should NOT have the sampling priority
        traceChunks[1]
           .Where(s => s.SpanId == span12.SpanId)
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == span1.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == null);
    }

    [Fact]
    public async Task MultipleChunks_1_and_3_Spans()
    {
        ISpan span1;
        ISpan span11;
        ISpan span12;
        ISpan span121;

        var propagatedContext = new SpanContext(traceId: 1, spanId: 10);
        var settings = new SpanCreationSettings { Parent = propagatedContext };

        using (var scope1 = _tracer.StartActive("1", settings))
        {
            span1 = scope1.Span;

            var traceContext = ((Scope)scope1).Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValue);

            using (var scope11 = _tracer.StartActive("1.1"))
            {
                span11 = scope11.Span;
            }

            // send the finished spans as one trace chunk
            traceContext.WriteClosedSpans();

            using (var scope12 = _tracer.StartActive("1.2"))
            {
                span12 = scope12.Span;

                using (var scope121 = _tracer.StartActive("1.2.1"))
                {
                    span121 = scope121.Span;
                }
            }
        }

        // send the remaining spans as another trace chunk
        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1 ]
        // [ 1.2.1, 1.2, 1 ]

        traceChunks.Should().HaveCount(2);    // 2 trace chunks
        traceChunks[0].Should().HaveCount(1); // 1 span
        traceChunks[1].Should().HaveCount(3); // 3 spans

        // chunk 0, orphan span should have the sampling priority
        traceChunks[0]
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.SpanId == span11.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);

        // chunk 1, orphan span should have the sampling priority
        traceChunks[1]
           .Where(s => s.SpanId == span1.SpanId)
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == propagatedContext.SpanId)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == SamplingPriorityValue);

        // chunk 1, other spans should NOT have the sampling priority
        traceChunks[1]
           .Where(s => s.SpanId != span1.SpanId)
           .Should()
           .HaveCount(2)
           .And.OnlyContain(s => s.ParentId > 0)
           .And.OnlyContain(s => s.GetMetric(SamplingPriorityName) == null);
    }

    [Fact]
    public async Task FourChunks_1_Span_Each()
    {
        ISpan span1;
        ISpan span11;
        ISpan span12;
        ISpan span121;

        var propagatedContext = new SpanContext(traceId: 1, spanId: 10);
        var settings = new SpanCreationSettings { Parent = propagatedContext };

        using (var scope1 = _tracer.StartActive("1", settings))
        {
            span1 = scope1.Span;

            var traceContext = ((Scope)scope1).Span.Context.TraceContext;
            traceContext.SetSamplingPriority(SamplingPriorityValue);

            using (var scope11 = _tracer.StartActive("1.1"))
            {
                span11 = scope11.Span;
            }

            // send the finished spans as one trace chunk
            traceContext.WriteClosedSpans();

            using (var scope12 = _tracer.StartActive("1.2"))
            {
                span12 = scope12.Span;

                using (var scope121 = _tracer.StartActive("1.2.1"))
                {
                    span121 = scope121.Span;
                }

                // send the finished spans as one trace chunk
                traceContext.WriteClosedSpans();
            }

            // send the finished spans as one trace chunk
            traceContext.WriteClosedSpans();
        }

        // send the remaining spans as another trace chunk
        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1 ]
        // [ 1.2.1 ]
        // [ 1.2 ]
        // [ 1 ]

        traceChunks.Should().HaveCount(4);                     // 4 trace chunks
        traceChunks.Should().OnlyContain(tc => tc.Count == 1); // 1 span each

        // chunk 0, orphan span should have the sampling priority
        var mockSpan11 = traceChunks[0][0];
        mockSpan11.SpanId.Should().Be(span11.SpanId);
        mockSpan11.ParentId.Should().Be(span1.SpanId);
        mockSpan11.Metrics.Should().Contain(SamplingPriorityName, SamplingPriorityValue);

        // chunk 1, orphan span should have the sampling priority
        var mockSpan121 = traceChunks[1][0];
        mockSpan121.SpanId.Should().Be(span121.SpanId);
        mockSpan121.ParentId.Should().Be(span12.SpanId);
        mockSpan121.Metrics.Should().Contain(SamplingPriorityName, SamplingPriorityValue);

        // chunk 2, orphan span should have the sampling priority
        var mockSpan12 = traceChunks[2][0];
        mockSpan12.SpanId.Should().Be(span12.SpanId);
        mockSpan12.ParentId.Should().Be(span1.SpanId);
        mockSpan12.Metrics.Should().Contain(SamplingPriorityName, SamplingPriorityValue);

        // chunk 3, orphan span should have the sampling priority
        var mockSpan1 = traceChunks[3][0];
        mockSpan1.SpanId.Should().Be(span1.SpanId);
        mockSpan1.ParentId.Should().Be(propagatedContext.SpanId);
        mockSpan1.Metrics.Should().Contain(SamplingPriorityName, SamplingPriorityValue);
    }
}
