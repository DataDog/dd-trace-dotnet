// <copyright file="SpanMessagePackFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class SpanMessagePackFormatterTests
{
    [Fact]
    public void SerializeSpans()
    {
        var formatter = SpanFormatterResolver.Instance.GetFormatter<TraceChunkModel>();

        var parentContext = new SpanContext(new TraceId(0, 1), 2, (int)SamplingPriority.UserKeep, "ServiceName1", "origin1");

        var spans = new[]
        {
            new Span(parentContext, DateTimeOffset.UtcNow),
            new Span(new SpanContext(parentContext, new TraceContext(Mock.Of<IDatadogTracer>()), "ServiceName1"), DateTimeOffset.UtcNow),
            new Span(new SpanContext(new TraceId(0, 5), 6, (int)SamplingPriority.UserKeep, "ServiceName3", "origin3"), DateTimeOffset.UtcNow),
        };

        spans[1].Tags.SetTag("Tag1", "Value1");
        spans[1].Tags.SetTag("Tag2", "Value1");
        spans[1].Tags.SetMetric("Metric1", 1.1);
        spans[1].Tags.SetMetric("Metric2", 2.1);
        spans[1].Tags.SetMetric("Metric3", 3.1);

        spans[2].Error = true;

        foreach (var span in spans)
        {
            span.SetDuration(TimeSpan.FromSeconds(1));
        }

        var traceChunk = new TraceChunkModel(new(spans));

        byte[] bytes = [];

        var length = formatter.Serialize(ref bytes, 0, traceChunk, SpanFormatterResolver.Instance);

        var result = global::MessagePack.MessagePackSerializer.Deserialize<MockSpan[]>(new ArraySegment<byte>(bytes, 0, length));

        result.Should().HaveCount(spans.Length);

        for (int i = 0; i < result.Length; i++)
        {
            var expected = spans[i];
            var actual = result[i];

            actual.TraceId.Should().Be(expected.TraceId);
            actual.SpanId.Should().Be(expected.SpanId);
            actual.Name.Should().Be(expected.OperationName);
            actual.Resource.Should().Be(expected.ResourceName);
            actual.Service.Should().Be(expected.ServiceName);
            actual.Type.Should().Be(expected.Type);
            actual.Start.Should().Be(expected.StartTime.ToUnixTimeNanoseconds());
            actual.Duration.Should().Be(expected.Duration.ToNanoseconds());
            actual.ParentId.Should().Be(expected.Context.ParentId);
            actual.Error.Should().Be(expected.Error ? (byte)0x1 : (byte)0x0);
            actual.ParentId.Should().Be(expected.Context.ParentIdInternal);

            var tagsProcessor = new TagsProcessor<string>(actual.Tags);
            expected.Tags.EnumerateTags(ref tagsProcessor);

            // runtime-id and language are added during serialization
            if (actual.ParentId == null)
            {
                tagsProcessor.Remaining.Should()
                    .HaveCount(2).And.Contain(new KeyValuePair<string, string>("runtime-id", RuntimeId.Get()), new KeyValuePair<string, string>("language", "dotnet"));
            }
            else
            {
                tagsProcessor.Remaining.Should()
                             .HaveCount(1).And.Contain(new KeyValuePair<string, string>("language", "dotnet"));
            }

            var metricsProcessor = new TagsProcessor<double>(actual.Metrics);
            expected.Tags.EnumerateMetrics(ref metricsProcessor);

            // process-id and _dd.top_level are added during serialization

            if (actual.ParentId == null)
            {
                metricsProcessor.Remaining.Should()
                .HaveCount(2).And.Contain(new KeyValuePair<string, double>("process_id", Process.GetCurrentProcess().Id), new KeyValuePair<string, double>("_dd.top_level", 1.0));
            }
            else
            {
                metricsProcessor.Remaining.Should().BeEmpty();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TraceId128_PropagatedTag(bool generate128BitTraceId)
    {
        var mockApi = new MockApi();
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, generate128BitTraceId } });
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: null, automaticFlush: false);
        var tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (_ = tracer.StartActive("root"))
        {
            using (_ = tracer.StartActive("child"))
            {
            }
        }

        await tracer.FlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));

        var span0 = traceChunks[0][0];
        var tagValue0 = span0.GetTag("_dd.p.tid");

        var span1 = traceChunks[0][1];
        var tagValue1 = span1.GetTag("_dd.p.tid");

        if (generate128BitTraceId)
        {
            // tag is added to first span of every chunk
            HexString.TryParseUInt64(tagValue0, out var traceIdUpperValue).Should().BeTrue();
            traceIdUpperValue.Should().BeGreaterThan(0);

            // not the second span
            tagValue1.Should().BeNull();
        }
        else
        {
            // tag is not added anywhere
            tagValue0.Should().BeNull();
            tagValue1.Should().BeNull();
        }
    }

    private readonly struct TagsProcessor<T> : IItemProcessor<T>
    {
        private readonly Dictionary<string, T> _expectedTags;

        public TagsProcessor(IEnumerable<KeyValuePair<string, T>> expectedTags)
        {
            _expectedTags = expectedTags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public IEnumerable<KeyValuePair<string, T>> Remaining => _expectedTags;

        public void Process(TagItem<T> item)
        {
            _expectedTags.Should().Contain(new KeyValuePair<string, T>(item.Key, item.Value));
            _expectedTags.Remove(item.Key);
        }
    }
}
