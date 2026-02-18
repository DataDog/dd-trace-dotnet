// <copyright file="SpanMessagePackFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.PlatformHelpers;
using Datadog.Trace.TestHelpers.Stats;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.Util;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class SpanMessagePackFormatterTests
{
    private readonly StubDatadogTracer _stubTracer = new();

    [Fact]
    public void SerializeSpans()
    {
        var formatter = SpanFormatterResolver.Instance.GetFormatter<TraceChunkModel>();
        var traceContext = new TraceContext(_stubTracer);
        var parentContext = new SpanContext(new TraceId(0, 1), 2, (int)SamplingPriority.UserKeep, "ServiceName1", "origin1");

        var spans = new[]
        {
            new Span(parentContext, DateTimeOffset.UtcNow),
            new Span(new SpanContext(parentContext, traceContext, "ServiceName1"), DateTimeOffset.UtcNow),
            new Span(new SpanContext(new TraceId(0, 5), 6, (int)SamplingPriority.UserKeep, "ServiceName3", "origin3"), DateTimeOffset.UtcNow),
        };

        spans[1].Tags.SetTag("Tag1", "Value1");
        spans[1].Tags.SetTag("Tag2", "Value1");
        spans[1].Tags.SetMetric("Metric1", 1.1);
        spans[1].Tags.SetMetric("Metric2", 2.1);
        spans[1].Tags.SetMetric("Metric3", 3.1);
        spans[1].Context.LastParentId = "0123456789abcdef";

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
            actual.ParentId.Should().Be(expected.Context.ParentId);

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
                if (!string.IsNullOrEmpty(expected.Context.LastParentId))
                {
                    tagsProcessor.Remaining.Should()
                                 .HaveCount(2).And.Contain(new KeyValuePair<string, string>("language", "dotnet"), new KeyValuePair<string, string>("_dd.parent_id", "0123456789abcdef"));
                }
                else
                {
                    tagsProcessor.Remaining.Should()
                                 .HaveCount(1).And.Contain(new KeyValuePair<string, string>("language", "dotnet"));
                }
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

    [Fact]
    public void SpanLink_Tag_Serialization()
    {
        var formatter = SpanFormatterResolver.Instance.GetFormatter<TraceChunkModel>();

        var parentContext = new SpanContext(new TraceId(0, 1), 2, (int)SamplingPriority.UserKeep, "ServiceName1", "origin1");

        var spans = new[]
        {
            new Span(parentContext, DateTimeOffset.UtcNow),
            new Span(new SpanContext(parentContext, new TraceContext(_stubTracer), "ServiceName1"), DateTimeOffset.UtcNow),
            new Span(new SpanContext(new TraceId(0, 5), 6, (int)SamplingPriority.UserKeep, "ServiceName3", "origin3"), DateTimeOffset.UtcNow),
        };
        var attributesToAdd = new List<KeyValuePair<string, string>>
        {
            new("link.name", "manually_linking"),
            new("pair", "false"),
            new("arbitrary", "56709")
        };
        spans[0].AddLink(new SpanLink(spans[1].Context, attributesToAdd));

        var tmpSpanLinkAttributesToAdd = new List<KeyValuePair<string, string>>
        {
            new("attribute1", "value1"),
            new("attribute2", "value2"),
        };
        var tmpSpanLink = new SpanLink(spans[2].Context, tmpSpanLinkAttributesToAdd);
        spans[1].AddLink(tmpSpanLink);

        spans[1].AddLink(new SpanLink(spans[0].Context));

        foreach (var span in spans)
        {
            span.SetDuration(TimeSpan.FromSeconds(1));
        }

        var traceChunk = new TraceChunkModel(new(spans));

        byte[] bytes = [];

        var length = formatter.Serialize(ref bytes, 0, traceChunk, SpanFormatterResolver.Instance);
        var result = global::MessagePack.MessagePackSerializer.Deserialize<MockSpan[]>(new ArraySegment<byte>(bytes, 0, length));

        for (int i = 0; i < result.Length; i++)
        {
            var expected = spans[i];
            var actual = result[i];

            if (expected.SpanLinks is not null)
            {
                for (int j = 0; j < expected.SpanLinks.Count; j++)
                {
                    var expectedSpanlink = expected.SpanLinks[j];
                    var actualSpanLink = actual.SpanLinks[j];
                    actualSpanLink.TraceIdHigh.Should().Be(expectedSpanlink.Context.TraceId128.Upper);
                    actualSpanLink.TraceIdLow.Should().Be(expectedSpanlink.Context.TraceId128.Lower);
                    actualSpanLink.SpanId.Should().Be(expectedSpanlink.Context.SpanId);
                    var expectedTraceState = W3CTraceContextPropagator.CreateTraceStateHeader(expectedSpanlink.Context);
                    // tracestate is only added when trace is from distributed tracing
                    if (expectedSpanlink.Context.IsRemote)
                    {
                        actualSpanLink.TraceState.Should().Be(expectedTraceState);
                    }

                    // 3 possible values, 1, 0 or null
                    var samplingPriority = expectedSpanlink.Context.TraceContext?.SamplingPriority ?? expectedSpanlink.Context.SamplingPriority;
                    var expectedTraceFlags = samplingPriority switch
                    {
                        null => 0u,             // not set
                        > 0 => 1u + (1u << 31), // keep
                        <= 0 => 1u << 31,       // drop
                    };
                    if (expectedTraceFlags > 0)
                    {
                        actualSpanLink.TraceFlags.Should().Be(expectedTraceFlags);
                    }

                    if (expectedSpanlink.Attributes is { Count: > 0 })
                    {
                        actualSpanLink.Attributes.Should().BeEquivalentTo(expectedSpanlink.Attributes);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public async Task SpanEvent_Tag_Serialization(bool? nativeSpanEventsEnabled)
    {
        var discoveryService = new DiscoveryServiceMock();
        var mockApi = new MockApi();
        var settings = TracerSettings.Create(new());
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null,  NullTelemetryController.Instance, discoveryService: discoveryService);

        tracer.TracerManager.Start();

        if (nativeSpanEventsEnabled is not null)
        {
            discoveryService.TriggerChange(spanEvents: (bool)nativeSpanEventsEnabled);
        }

        var formatter = SpanFormatterResolver.Instance.GetFormatter<TraceChunkModel>();

        var parentContext = new SpanContext(new TraceId(0, 1), 2, (int)SamplingPriority.UserKeep, "ServiceName1", "origin1");
        var traceContext = new TraceContext(tracer);
        var spanContext = new SpanContext(parentContext, traceContext, "ServiceName1");
        var span = new Span(spanContext, DateTimeOffset.UtcNow);

        var eventName = "test_event";
        var eventTimestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var eventAttributes = new List<KeyValuePair<string, object>>
        {
            new("string_key", "hello"),
            new("char_key", 'c'),
            new("bool_key", true),
            new("int_key", 42),
            new("uint_key", 420U),
            new("byte_key", (byte)7),
            new("sbyte_key", (sbyte)-7),
            new("short_key", (short)30000),
            new("ushort_key", (ushort)60000),
            new("float_key", 1.23f),
            new("double_key", 3.14),
            new("decimal_key", 1.23m),
            new("ulong_key", 12345678901234567890),
            new("empty_value", null),
            new("string_array", new[] { "item1", "item2", "item3" }),
        };

        var eventAttributes2 = new List<KeyValuePair<string, object>>
        {
            new("object_array", new object[] { "string", 42, true }),
            new("bool_array", new[] { true, false, true }),
            new("int_array", new[] { 123, 1234, 12345 }),
            new("double_array", new[] { 1.2, 1.3210, 200000.1 }),
            new(null, "empty_key"),
            new("string_key", "hello"),
        };

        span.AddEvent(new SpanEvent(eventName, eventTimestamp, eventAttributes));
        span.AddEvent(new SpanEvent("another_event", eventTimestamp.AddSeconds(1), eventAttributes2));
        span.SetDuration(TimeSpan.FromSeconds(1));

        var traceChunk = new TraceChunkModel(new([span]));
        byte[] bytes = [];
        var length = formatter.Serialize(ref bytes, 0, traceChunk, SpanFormatterResolver.Instance);
        var result = global::MessagePack.MessagePackSerializer.Deserialize<MockSpan[]>(new ArraySegment<byte>(bytes, 0, length));

        result.Should().HaveCount(1);
        var deserializedSpan = result[0];

        if (nativeSpanEventsEnabled is true)
        {
            deserializedSpan.SpanEvents.Should().HaveCount(2);

            var firstEvent = deserializedSpan.SpanEvents[0];
            firstEvent.Name.Should().Be(eventName);
            firstEvent.Timestamp.Should().Be(eventTimestamp.ToUnixTimeNanoseconds());

            var attributes = firstEvent.Attributes;

            attributes.Should().NotContainKey("decimal_key");
            attributes.Should().NotContainKey("ulong_key");
            attributes.Should().NotContainKey("empty_value");

            attributes["string_key"].Type.Should().Be(0);
            attributes["string_key"].StringValue.Should().Be("hello");
            attributes["string_key"].BoolValue.Should().BeNull();
            attributes["string_key"].IntValue.Should().BeNull();
            attributes["string_key"].DoubleValue.Should().BeNull();

            attributes["bool_key"].Type.Should().Be(1);
            attributes["bool_key"].BoolValue.Should().Be(true);
            attributes["bool_key"].StringValue.Should().BeNull();
            attributes["bool_key"].IntValue.Should().BeNull();
            attributes["bool_key"].DoubleValue.Should().BeNull();

            attributes["int_key"].Type.Should().Be(2);
            attributes["int_key"].IntValue.Should().Be(42);
            attributes["int_key"].StringValue.Should().BeNull();
            attributes["int_key"].BoolValue.Should().BeNull();
            attributes["int_key"].DoubleValue.Should().BeNull();

            attributes["double_key"].Type.Should().Be(3);
            attributes["double_key"].DoubleValue.Should().Be(3.14);
            attributes["double_key"].StringValue.Should().BeNull();
            attributes["double_key"].BoolValue.Should().BeNull();
            attributes["double_key"].IntValue.Should().BeNull();

            attributes["char_key"].Type.Should().Be(0);
            attributes["char_key"].StringValue.Should().Be("c");

            attributes["uint_key"].Type.Should().Be(2);
            attributes["uint_key"].IntValue.Should().Be(420);

            attributes["byte_key"].Type.Should().Be(2);
            attributes["byte_key"].IntValue.Should().Be(7);

            attributes["sbyte_key"].Type.Should().Be(2);
            attributes["sbyte_key"].IntValue.Should().Be(-7);

            attributes["short_key"].Type.Should().Be(2);
            attributes["short_key"].IntValue.Should().Be(30000);

            attributes["ushort_key"].Type.Should().Be(2);
            attributes["ushort_key"].IntValue.Should().Be(60000);

            attributes["float_key"].Type.Should().Be(3);
            attributes["float_key"].DoubleValue.Should().BeApproximately(1.23, 0.001);

            var arrayAttr = attributes["string_array"];
            arrayAttr.Type.Should().Be(4);
            arrayAttr.ArrayValue.Values.Should().AllSatisfy(item => item.Type.Should().Be(0)); // string type
            arrayAttr.ArrayValue.Values[0].StringValue.Should().Be("item1");
            arrayAttr.ArrayValue.Values[1].StringValue.Should().Be("item2");
            arrayAttr.ArrayValue.Values[2].StringValue.Should().Be("item3");
            arrayAttr.StringValue.Should().BeNull();
            arrayAttr.BoolValue.Should().BeNull();
            arrayAttr.IntValue.Should().BeNull();
            arrayAttr.DoubleValue.Should().BeNull();

            var secondEvent = deserializedSpan.SpanEvents[1];
            secondEvent.Name.Should().Be("another_event");
            secondEvent.Timestamp.Should().Be(eventTimestamp.AddSeconds(1).ToUnixTimeNanoseconds());

            var attributes2 = secondEvent.Attributes;
            attributes2.Should().HaveCount(4);

            attributes2.Should().NotContainNulls();
            attributes2.Should().NotContainKey("object_array");

            attributes2["string_key"].Type.Should().Be(0);
            attributes2["string_key"].StringValue.Should().Be("hello");
            attributes2["string_key"].BoolValue.Should().BeNull();
            attributes2["string_key"].IntValue.Should().BeNull();
            attributes2["string_key"].DoubleValue.Should().BeNull();

            var boolArray = attributes2["bool_array"];
            boolArray.Type.Should().Be(4);
            boolArray.ArrayValue.Values.Should().HaveCount(3);
            boolArray.ArrayValue.Values.Should().AllSatisfy(item => item.Type.Should().Be(1)); // bool type
            boolArray.ArrayValue.Values[0].BoolValue.Should().Be(true);
            boolArray.ArrayValue.Values[1].BoolValue.Should().Be(false);
            boolArray.ArrayValue.Values[2].BoolValue.Should().Be(true);
            boolArray.StringValue.Should().BeNull();
            boolArray.BoolValue.Should().BeNull();
            boolArray.IntValue.Should().BeNull();
            boolArray.DoubleValue.Should().BeNull();

            var intArray = attributes2["int_array"];
            intArray.Type.Should().Be(4);
            intArray.ArrayValue.Values.Should().HaveCount(3);
            intArray.ArrayValue.Values.Should().AllSatisfy(item => item.Type.Should().Be(2)); // int type
            intArray.ArrayValue.Values[0].IntValue.Should().Be(123);
            intArray.ArrayValue.Values[1].IntValue.Should().Be(1234);
            intArray.ArrayValue.Values[2].IntValue.Should().Be(12345);
            intArray.StringValue.Should().BeNull();
            intArray.BoolValue.Should().BeNull();
            intArray.IntValue.Should().BeNull();
            intArray.DoubleValue.Should().BeNull();

            var doubleArray = attributes2["double_array"];
            doubleArray.Type.Should().Be(4);
            doubleArray.ArrayValue.Values.Should().HaveCount(3);
            doubleArray.ArrayValue.Values.Should().AllSatisfy(item => item.Type.Should().Be(3)); // double type
            doubleArray.ArrayValue.Values[0].DoubleValue.Should().Be(1.2);
            doubleArray.ArrayValue.Values[1].DoubleValue.Should().Be(1.321);
            doubleArray.ArrayValue.Values[2].DoubleValue.Should().Be(200000.1);
            doubleArray.StringValue.Should().BeNull();
            doubleArray.BoolValue.Should().BeNull();
            doubleArray.IntValue.Should().BeNull();
            doubleArray.DoubleValue.Should().BeNull();
        }
        else
        {
            deserializedSpan.SpanEvents.Should().BeNullOrEmpty();
            deserializedSpan.Tags.Should().ContainKey("events");
            var eventsJson = deserializedSpan.Tags["events"];

            eventsJson.Should().Contain($"\"name\":\"{eventName}\"");
            eventsJson.Should().Contain($"\"time_unix_nano\":{eventTimestamp.ToUnixTimeNanoseconds()}");

            var firstEventExpectedAttributes = new[]
            {
                "\"string_key\":\"hello\"",
                "\"bool_key\":true",
                "\"int_key\":42",
                "\"double_key\":3.14",
                "\"char_key\":\"c\"",
                "\"uint_key\":420",
                "\"byte_key\":7",
                "\"sbyte_key\":-7",
                "\"short_key\":30000",
                "\"ushort_key\":60000",
                "\"float_key\":1.23",
                "\"string_array\":[\"item1\",\"item2\",\"item3\"]"
            };
            firstEventExpectedAttributes.Should().AllSatisfy(attr => eventsJson.Should().Contain(attr));

            eventsJson.Should().Contain("\"name\":\"another_event\"");
            eventsJson.Should().Contain($"\"time_unix_nano\":{eventTimestamp.AddSeconds(1).ToUnixTimeNanoseconds()}");

            var secondEventExpectedAttributes = new[]
            {
                "\"bool_array\":[true,false,true]",
                "\"int_array\":[123,1234,12345]",
                "\"double_array\":[1.2,1.321,200000.1]",
                "\"string_key\":\"hello\""
            };
            secondEventExpectedAttributes.Should().AllSatisfy(attr => eventsJson.Should().Contain(attr));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TraceId128_PropagatedTag(bool generate128BitTraceId)
    {
        var mockApi = new MockApi();
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, generate128BitTraceId } });
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

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

    [Fact]
    public async Task LastParentId_Tag()
    {
        var mockApi = new MockApi();
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, false } });
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (var scope = tracer.StartActiveInternal("root"))
        {
            scope.Span.Context.LastParentId = "0123456789abcdef";
        }

        await tracer.FlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));

        var span0 = traceChunks[0][0];
        var tagValue0 = span0.GetTag("_dd.parent_id");

        tagValue0.Should().Be("0123456789abcdef");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessTags_Serialization(bool propagateProcessTags)
    {
        var mockApi = new MockApi();
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.PropagateProcessTags, propagateProcessTags.ToString() },
            { ConfigurationKeys.ServiceName, "test-service" }
        });
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (_ = tracer.StartActive("root"))
        {
            using (_ = tracer.StartActive("child1"))
            {
            }

            using (_ = tracer.StartActive("child2"))
            {
            }
        }

        await tracer.FlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(1));

        traceChunks.Should().HaveCount(1);
        var spans = traceChunks[0];
        spans.Should().HaveCount(3);

        var firstSpan = spans[0];
        var secondSpan = spans[1];
        var thirdSpan = spans[2];

        if (propagateProcessTags)
        {
            // Process tags should be present only in the first span
            var processTagsValue = firstSpan.GetTag(Tags.ProcessTags);
            processTagsValue.Should().NotBeNullOrEmpty("process tags should be in the first span when enabled");
            processTagsValue.Should().Contain(ProcessTags.EntrypointBasedir);
            processTagsValue.Should().Contain(ProcessTags.EntrypointWorkdir);
            processTagsValue.Should().Contain("svc.user:true");

            // Should not be in subsequent spans
            secondSpan.GetTag(Tags.ProcessTags).Should().BeNull("process tags should only be in the first span");
            thirdSpan.GetTag(Tags.ProcessTags).Should().BeNull("process tags should only be in the first span");
        }
        else
        {
            // When disabled, process tags should not be present in any span
            firstSpan.GetTag(Tags.ProcessTags).Should().BeNull("process tags should not be present when disabled");
            secondSpan.GetTag(Tags.ProcessTags).Should().BeNull("process tags should not be present when disabled");
            thirdSpan.GetTag(Tags.ProcessTags).Should().BeNull("process tags should not be present when disabled");
        }
    }

    [Fact]
    public async Task AllCachedValues_AreCorrectlySerialized()
    {
        // This test verifies that all cached values in SpanMessagePackFormatter are correctly
        // initialized and serialized. This includes:
        // - 11 AAS tag values (moved from MessagePackStringCache in optimization)
        // - 2 git tag values (moved from MessagePackStringCache in optimization)
        // - Constant tag names and values (language, runtime-id, etc.)

        var mockApi = new MockApi();

        // Use AzureAppServiceHelper to get proper AAS configuration
        var aasConfig = AzureAppServiceHelper.GetRequiredAasConfigurationValues(
            subscriptionId: "test-sub-id",
            deploymentId: "test-site",
            planResourceGroup: "test-plan-rg",
            siteResourceGroup: "test-rg");

        // Add git metadata and other configuration values
        var configValues = new Dictionary<string, string>
        {
            { "DD_GIT_COMMIT_SHA", "abc123def456" },
            { "DD_GIT_REPOSITORY_URL", "https://github.com/test/repo" },
            { ConfigurationKeys.Environment, "test-env" },
            { ConfigurationKeys.ServiceVersion, "1.2.3" }
        };

        // Combine AAS config with additional config values
        var compositeSource = new CompositeConfigurationSource
        {
            aasConfig,
            new DictionaryConfigurationSource(configValues)
        };

        var settings = new TracerSettings(compositeSource);
        var agentWriter = new AgentWriter(mockApi, statsAggregator: null, statsd: TestStatsdManager.NoOp, automaticFlush: false);
        await using var tracer = TracerHelper.Create(settings, agentWriter, sampler: null, scopeManager: null, statsd: null, NullTelemetryController.Instance, NullDiscoveryService.Instance);

        using (_ = tracer.StartActive("test-operation"))
        {
        }

        await tracer.FlushAsync();
        var traceChunks = mockApi.Wait(TimeSpan.FromSeconds(30));

        traceChunks.Should().HaveCount(1);
        var spans = traceChunks[0];
        spans.Should().HaveCount(1);
        var span = spans[0];

        // ===== Verify all 11 AAS tag VALUES (optimized to use cached bytes) =====
        // These were moved from MessagePackStringCache to SpanMessagePackFormatter fields
        span.GetTag(Tags.AzureAppServicesSiteName).Should().Be("test-site", "aas.site.name value should be cached");
        span.GetTag(Tags.AzureAppServicesSiteKind).Should().Be("app", "aas.site.kind value should be cached");
        span.GetTag(Tags.AzureAppServicesSiteType).Should().Be("app", "aas.site.type value should be cached");
        span.GetTag(Tags.AzureAppServicesResourceGroup).Should().Be("test-rg", "aas.resource.group value should be cached");
        span.GetTag(Tags.AzureAppServicesSubscriptionId).Should().Be("test-sub-id", "aas.subscription.id value should be cached");
        span.GetTag(Tags.AzureAppServicesResourceId).Should().NotBeNullOrEmpty("aas.resource.id value should be cached");
        span.GetTag(Tags.AzureAppServicesResourceId).Should().Contain("test-sub-id").And.Contain("test-rg");
        // AzureAppServiceHelper hardcodes these values (see AzureAppServiceHelper.GetRequiredAasConfigurationValues)
        span.GetTag(Tags.AzureAppServicesInstanceId).Should().Be("instance_id", "aas.instance.id value should be cached");
        span.GetTag(Tags.AzureAppServicesInstanceName).Should().Be("instance_name", "aas.instance.name value should be cached");
        span.GetTag(Tags.AzureAppServicesOperatingSystem).Should().NotBeNullOrEmpty("aas.environment.os value should be cached");
        span.GetTag(Tags.AzureAppServicesRuntime).Should().NotBeNullOrEmpty("aas.environment.runtime value should be cached");
        span.GetTag(Tags.AzureAppServicesExtensionVersion).Should().Be("3.0.0", "aas.environment.extension_version value should be cached");

        // ===== Verify git tag VALUES (optimized to use cached bytes) =====
        // These were moved from MessagePackStringCache to SpanMessagePackFormatter fields
        span.GetTag(Tags.GitCommitSha).Should().Be("abc123def456", "git.commit.sha value should be cached");
        span.GetTag(Tags.GitRepositoryUrl).Should().Be("https://github.com/test/repo", "git.repository_url value should be cached");

        // ===== Verify other constant cached values =====
        span.GetTag(Tags.Language).Should().Be(TracerConstants.Language, "language tag should be 'dotnet'");
        span.GetTag(Tags.RuntimeId).Should().NotBeNullOrEmpty("runtime-id should be present and cached");
        span.GetTag(Tags.Env).Should().Be("test-env", "env tag should match configured value");
        span.GetTag(Tags.Version).Should().Be("1.2.3", "version tag should match configured value");

        // ===== Verify metrics (numeric tags) that use cached names =====
        span.Metrics.Should().ContainKey(Datadog.Trace.Metrics.ProcessId, "process_id metric should be present with cached name");
        span.Metrics[Datadog.Trace.Metrics.ProcessId].Should().BeGreaterThan(0, "process_id should be valid");

        // ===== Verify span fields that use cached names =====
        span.Service.Should().NotBeNullOrEmpty("service name should be present");
        span.Name.Should().Be("test-operation", "operation name should match");
        span.Resource.Should().Be("test-operation", "resource should match operation name");
        span.TraceId.Should().BeGreaterThan(0, "trace_id should be valid");
        span.SpanId.Should().BeGreaterThan(0, "span_id should be valid");
        span.ParentId.Should().BeNull("root span should have no parent_id");
        span.Error.Should().Be(0, "error flag should be 0 for successful span");
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
