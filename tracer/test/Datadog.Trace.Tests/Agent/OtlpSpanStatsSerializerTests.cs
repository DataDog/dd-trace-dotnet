// <copyright file="OtlpSpanStatsSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class OtlpSpanStatsSerializerTests
    {
        private const long BucketDurationNs = 10_000_000_000L; // 10s

        private static readonly List<byte[]> EmptyPeerTags = [];

        [Fact]
        public void Serialize_ReturnsNull_WhenNoHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 0 });

            OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs).Should().BeNull();
        }

        [Fact]
        public void SerializeJson_ReturnsNull_WhenNoHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 0 });

            OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs).Should().BeNull();
        }

        [Fact]
        public void Serialize_ReturnsNonNull_WhenHasHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 5, Duration = 100_000_000 });

            OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs).Should().NotBeNull();
        }

        [Fact]
        public void SerializeJson_ReturnsNonNull_WhenHasHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 5, Duration = 100_000_000 });

            OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs).Should().NotBeNull();
        }

        [Fact]
        public void SerializeJson_ContainsMetricName()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].name")!
                .Value<string>().Should().Be(OtlpSpanStatsSerializer.MetricName);
        }

        [Fact]
        public void SerializeJson_TimestampsAreQuotedStrings()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]")!;

            dp["startTimeUnixNano"]!.Type.Should().Be(JTokenType.String);
            dp["timeUnixNano"]!.Type.Should().Be(JTokenType.String);
        }

        [Fact]
        public void SerializeJson_CountIsQuotedString()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]")!;

            dp["count"]!.Type.Should().Be(JTokenType.String);
        }

        [Fact]
        public void SerializeJson_Has16ExplicitBounds()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var bounds = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0].explicitBounds")!;

            bounds.Should().HaveCount(16);
        }

        [Fact]
        public void SerializeJson_Has17BucketCounts()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var counts = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0].bucketCounts")!;

            counts.Should().HaveCount(17);
        }

        [Fact]
        public void SerializeJson_BucketCountsAreQuotedStrings()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var counts = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0].bucketCounts")!;

            foreach (var token in counts)
            {
                token.Type.Should().Be(JTokenType.String);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_AttributesFollowContract(bool useJson)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isSyntheticsRequest: true, grpcStatusCode: "5", serviceSource: "component");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = useJson ? GetDataPointAttributes(SerializeToJson(buffer)) : GetProtobufDataPointAttributes(buffer);

            attrs.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["service.name"] = "my-service",
                ["status.code"] = "STATUS_CODE_OK",
                ["span.kind"] = "SPAN_KIND_SERVER",
                ["span.name"] = "GET /",
                ["http.request.method"] = "GET",
                ["http.response.status_code"] = "200",
                ["http.route"] = "/api/v1",
                ["rpc.response.status_code"] = "NOT_FOUND",
                ["datadog.operation.name"] = "http.request",
                ["datadog.span.type"] = "web",
                ["datadog.span.top_level"] = "true",
                ["datadog.is_trace_root"] = "true",
                ["datadog.origin"] = "synthetics",
                ["datadog.svc_src"] = "component",
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_AlwaysEmitsOperationName(bool useJson)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(operationName: string.Empty);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = useJson ? GetDataPointAttributes(SerializeToJson(buffer)) : GetProtobufDataPointAttributes(buffer);

            attrs.Should().ContainKey("datadog.operation.name").WhoseValue.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false, true, "true")]
        [InlineData(false, false, "false")]
        [InlineData(false, null, null)]
        [InlineData(true, true, "true")]
        [InlineData(true, false, "false")]
        [InlineData(true, null, null)]
        public void Serialize_EmitsKnownIsTraceRoot(bool useJson, bool? isTraceRoot, string? expected)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isTraceRoot: isTraceRoot);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = useJson ? GetDataPointAttributes(SerializeToJson(buffer)) : GetProtobufDataPointAttributes(buffer);

            if (expected is null)
            {
                attrs.Should().NotContainKey("datadog.is_trace_root");
            }
            else
            {
                attrs.Should().ContainKey("datadog.is_trace_root").WhoseValue.Should().Be(expected);
            }
        }

        [Fact]
        public void SerializeJson_UsesBoolValues()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isTopLevel: false, isTraceRoot: true);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer);
            var topLevelValue = GetJsonDataPointAttributeValue(json, "datadog.span.top_level");
            var traceRootValue = GetJsonDataPointAttributeValue(json, "datadog.is_trace_root");

            topLevelValue["boolValue"]!.Type.Should().Be(JTokenType.Boolean);
            topLevelValue["boolValue"]!.Value<bool>().Should().BeFalse();
            traceRootValue["boolValue"]!.Type.Should().Be(JTokenType.Boolean);
            traceRootValue["boolValue"]!.Value<bool>().Should().BeTrue();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_ErrorDataPoint_IncludesStatusCode(bool useJson)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isError: true);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = useJson ? GetDataPointAttributes(SerializeToJson(buffer)) : GetProtobufDataPointAttributes(buffer);

            attrs.Should().ContainKey("status.code").WhoseValue.Should().Be("STATUS_CODE_ERROR");
        }

        [Fact]
        public void SerializeJson_MinMaxWritten_WhenSet()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            var bucket = new StatsBucket(key, EmptyPeerTags, [])
            {
                Hits = 1,
                Duration = 5_000_000_000L,
                MinDuration = 1_000_000L,  // 1ms in ns → 0.001s
                MaxDuration = 50_000_000L, // 50ms in ns → 0.05s
            };
            buffer.Buckets.Add(key, bucket);

            var json = SerializeToJson(buffer);
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]")!;

            ((double)dp["min"]!).Should().Be(0.001);
            ((double)dp["max"]!).Should().Be(0.05);
        }

        [Fact]
        public void SerializeJson_MinMaxAbsent_WhenNotObserved()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            var bucket = new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 };
            // MinDuration sentinel is long.MaxValue, MaxDuration sentinel is long.MinValue
            buffer.Buckets.Add(key, bucket);

            var json = SerializeToJson(buffer);
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]")!;

            dp["min"].Should().BeNull();
            dp["max"].Should().BeNull();
        }

        [Fact]
        public void SerializeJson_MinMaxWritten_WhenDurationIsZero()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            // A span whose observed duration is exactly 0 (e.g. clamped clock skew): min/max
            // must still be emitted as 0, distinct from the "never observed" sentinels.
            var bucket = new StatsBucket(key, EmptyPeerTags, [])
            {
                Hits = 1,
                Duration = 0,
                MinDuration = 0,
                MaxDuration = 0,
            };
            buffer.Buckets.Add(key, bucket);

            var json = SerializeToJson(buffer);
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]")!;

            ((double)dp["min"]!).Should().Be(0);
            ((double)dp["max"]!).Should().Be(0);
        }

        [Fact]
        public void SerializeJson_ResourceAttributes_IncludeServiceName()
        {
            var buffer = CreateBuffer(service: "my-service");
            AddHit(buffer);

            var json = SerializeToJson(buffer);
            var resourceAttrs = GetResourceAttributes(json);

            resourceAttrs.Should().ContainKey("service.name").WhoseValue.Should().Be("my-service");
        }

        [Fact]
        public void SerializeJson_ResourceIncludesRuntimeId()
        {
            var buffer = CreateBuffer();
            AddHit(buffer);

            var json = SerializeToJson(buffer);
            var resourceAttrs = GetResourceAttributes(json);

            resourceAttrs.Should().ContainKey("datadog.runtime_id");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_ResourceIncludesProcessTags(bool useJson)
        {
            var buffer = CreateBuffer();
            AddHit(buffer);

            var processTagValues = useJson
                                       ? SerializeToJson(buffer)
                                        .SelectTokens("$..attributes[?(@.key == 'datadog.process_tags')].value.arrayValue.values[*].stringValue")
                                        .Values<string>()
                                       : GetProtobufResourceAttributeValues(buffer)["datadog.process_tags"].ArrayValue.Values.Select(value => value.StringValue);

            processTagValues.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_EmitsAdditionalMetricTags(bool useJson)
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            var additionalMetricTags = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("team:payments"),
                Encoding.UTF8.GetBytes("datadog.custom:value"),
                Encoding.UTF8.GetBytes("endpoint:https://example.com:443"),
                Encoding.UTF8.GetBytes("span.kind:custom"),
                Encoding.UTF8.GetBytes(StatsAggregator.BlockedByTracerSentinel),
            };
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, additionalMetricTags) { Hits = 1, Duration = 5_000_000 });

            var attrs = useJson ? GetDataPointAttributes(SerializeToJson(buffer)) : GetProtobufDataPointAttributes(buffer);

            attrs.Should().ContainKey("team").WhoseValue.Should().Be("payments");
            attrs.Should().ContainKey("datadog.custom").WhoseValue.Should().Be("value");
            attrs.Should().ContainKey("endpoint").WhoseValue.Should().Be("https://example.com:443");
            attrs.Should().ContainKey("span.kind").WhoseValue.Should().Be("SPAN_KIND_SERVER");
            attrs.Should().ContainKey(StatsAggregator.BlockedByTracerSentinel).WhoseValue.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_PeerTags_EmittedAsCombinedArrayValue(bool useJson)
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            var peerTags = new List<byte[]>
            {
                Encoding.UTF8.GetBytes("peer.service:downstream"),
                Encoding.UTF8.GetBytes("net.peer.name:downstream.example.com"),
            };
            buffer.Buckets.Add(key, new StatsBucket(key, peerTags, []) { Hits = 1, Duration = 5_000_000 });

            var peerTagValues = useJson
                                    ? SerializeToJson(buffer)
                                     .SelectTokens("$..attributes[?(@.key == 'datadog.peer_tags')].value.arrayValue.values[*].stringValue")
                                     .Values<string>()
                                    : GetProtobufDataPointAttributeValues(buffer)["datadog.peer_tags"].ArrayValue.Values.Select(value => value.StringValue);

            peerTagValues.Should().Equal("peer.service:downstream", "net.peer.name:downstream.example.com");
        }

        [Fact]
        public void SerializeJson_PeerTags_AbsentWhenEmpty()
        {
            var attrs = GetDataPointAttributes(SerializeToJson(CreateBufferWithOneHit()));

            attrs.Should().NotContainKey("datadog.peer_tags");
        }

        [Fact]
        public void SerializeJson_AggregationTemporalityIsDelta()
        {
            var json = SerializeToJson(CreateBufferWithOneHit());
            var temporality = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.aggregationTemporality")!;

            temporality.Value<int>().Should().Be(1); // DELTA = 1
        }

        [Fact]
        public void Serialize_ProducesBytes_NotEmpty()
        {
            var bytes = OtlpSpanStatsSerializer.Serialize(CreateBufferWithOneHit(), BucketDurationNs);

            bytes.Should().NotBeNullOrEmpty();
            bytes!.Length.Should().BeGreaterThan(10);
        }

        [Fact]
        public void Serialize_Protobuf_StartsWithResourceMetricsFieldTag()
        {
            // ExportMetricsServiceRequest field 1, wire type 2 (length-delimited) → tag byte = (1 << 3) | 2 = 0x0A
            var bytes = OtlpSpanStatsSerializer.Serialize(CreateBufferWithOneHit(), BucketDurationNs)!;

            bytes[0].Should().Be(0x0A);
        }

        [Fact]
        public void Serialize_Protobuf_UsesBoolValues()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isTopLevel: false, isTraceRoot: true);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var values = GetProtobufDataPointAttributeValues(buffer);

            values["datadog.span.top_level"].ValueCase.Should().Be(AnyValue.ValueOneofCase.BoolValue);
            ReadAnyValue(values["datadog.span.top_level"]).Should().Be("false");
            values["datadog.is_trace_root"].ValueCase.Should().Be(AnyValue.ValueOneofCase.BoolValue);
            ReadAnyValue(values["datadog.is_trace_root"]).Should().Be("true");
        }

        [Theory]
        [InlineData("5", "NOT_FOUND")]
        [InlineData("0", "OK")]
        [InlineData("14", "UNAVAILABLE")]
        [InlineData("16", "UNAUTHENTICATED")]
        [InlineData("NOT_FOUND", "NOT_FOUND")]
        [InlineData("not_found", "NOT_FOUND")]
        [InlineData("OK", "OK")]
        [InlineData("ok", "OK")]
        [InlineData("CANCELED", "CANCELLED")]
        [InlineData("NOTFOUND", "NOT_FOUND")]
        [InlineData("StatusCode.NotFound", "NOT_FOUND")]
        [InlineData("StatusCode.OK", "OK")]
        public void SerializeJson_GrpcStatusCode_EmitsCanonicalStringName(string input, string expected)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(grpcStatusCode: input);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = GetDataPointAttributes(SerializeToJson(buffer));

            attrs.Should().ContainKey("rpc.response.status_code").WhoseValue.Should().Be(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData("999")]
        [InlineData("garbage")]
        [InlineData("-1")]
        public void SerializeJson_GrpcStatusCode_AbsentWhenInvalid(string input)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(grpcStatusCode: input);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = GetDataPointAttributes(SerializeToJson(buffer));

            attrs.Should().NotContainKey("rpc.response.status_code");
        }

        [Theory]
        [InlineData("server", "SPAN_KIND_SERVER")]
        [InlineData("SeRvEr", "SPAN_KIND_SERVER")]
        [InlineData("client", "SPAN_KIND_CLIENT")]
        [InlineData("producer", "SPAN_KIND_PRODUCER")]
        [InlineData("consumer", "SPAN_KIND_CONSUMER")]
        [InlineData("internal", "SPAN_KIND_INTERNAL")]
        [InlineData("", "SPAN_KIND_INTERNAL")]
        [InlineData("not-a-real-kind", "SPAN_KIND_INTERNAL")]
        public void SerializeJson_SpanKind_EmitsCanonicalUppercaseName(string input, string expected)
        {
            var buffer = CreateBuffer();
            var key = CreateKey(spanKind: input);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = GetDataPointAttributes(SerializeToJson(buffer));

            attrs.Should().ContainKey("span.kind").WhoseValue.Should().Be(expected);
        }

        [Fact]
        public void SerializeProtobuf_SpanKind_DefaultsToInternalWhenEmpty()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(spanKind: string.Empty);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = GetProtobufDataPointAttributes(buffer);

            attrs.Should().ContainKey("span.kind").WhoseValue.Should().Be("SPAN_KIND_INTERNAL");
        }

        [Fact]
        public void SerializeJson_NoRpcMethodAttribute()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(grpcStatusCode: "5");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var attrs = GetDataPointAttributes(SerializeToJson(buffer));

            attrs.Should().NotContainKey("rpc.method");
        }

        [Fact]
        public void SerializeJson_TopLevelAndNonTopLevel_ProduceSeparateDataPoints()
        {
            var buffer = CreateBuffer();
            var topKey = CreateKey(isTopLevel: true);
            var nonTopKey = CreateKey(isTopLevel: false);
            buffer.Buckets.Add(topKey, new StatsBucket(topKey, EmptyPeerTags, []) { Hits = 1, Duration = 1_000_000 });
            buffer.Buckets.Add(nonTopKey, new StatsBucket(nonTopKey, EmptyPeerTags, []) { Hits = 1, Duration = 1_000_000 });

            var json = SerializeToJson(buffer);
            var dataPoints = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints")!;

            dataPoints.Should().HaveCount(2);

            var topLevelValues = dataPoints
                .Select(dp => GetDataPointAttributesFromToken((JObject)dp))
                .Where(a => a.ContainsKey("datadog.span.top_level"))
                .Select(a => a["datadog.span.top_level"])
                .ToList();

            topLevelValues.Should().Contain("true");
            topLevelValues.Should().Contain("false");
        }

        private static StatsBuffer CreateBuffer(string service = "my-service", string env = "prod", string version = "1.0")
        {
            var settings = MutableSettings.CreateForTesting(
                new(),
                new Dictionary<string, object?>
                {
                    { ConfigurationKeys.ServiceName, service },
                    { ConfigurationKeys.Environment, env },
                    { ConfigurationKeys.ServiceVersion, version },
                });
            return new StatsBuffer(new ClientStatsPayload(settings), new StatsCardinalityLimiter(new TracerSettings()), new StatsCardinalityReporter(NullMetricsTelemetryCollector.Instance));
        }

        private static StatsAggregationKey CreateKey(
            string resource = "GET /",
            string service = "my-service",
            string operationName = "http.request",
            string type = "web",
            int httpStatusCode = 200,
            bool isSyntheticsRequest = false,
            string spanKind = "server",
            bool isError = false,
            bool isTopLevel = true,
            bool? isTraceRoot = true,
            string httpMethod = "GET",
            string httpEndpoint = "/api/v1",
            string grpcStatusCode = "",
            string serviceSource = "",
            ulong peerTagsHash = 0)
        {
            return new StatsAggregationKey(
                resource,
                service,
                operationName,
                type,
                httpStatusCode,
                isSyntheticsRequest,
                spanKind,
                isError,
                isTopLevel,
                isTraceRoot,
                httpMethod,
                httpEndpoint,
                grpcStatusCode,
                serviceSource,
                peerTagsHash,
                additionalMetricTagsHash: 0,
                truncatedFields: StatsCardinalityTruncatedFields.None);
        }

        private static StatsBuffer CreateBufferWithOneHit(string service = "my-service")
        {
            var buffer = CreateBuffer(service: service);
            AddHit(buffer);
            return buffer;
        }

        private static void AddHit(StatsBuffer buffer, string service = "my-service")
        {
            var key = CreateKey(service: service);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });
        }

        private static JObject SerializeToJson(StatsBuffer buffer)
        {
            var bytes = OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs)!;
            return JObject.Parse(Encoding.UTF8.GetString(bytes));
        }

        private static Dictionary<string, string> GetProtobufDataPointAttributes(StatsBuffer buffer)
        {
            return GetProtobufDataPointAttributeValues(buffer)
                  .ToDictionary(kvp => kvp.Key, kvp => ReadAnyValue(kvp.Value));
        }

        private static Dictionary<string, AnyValue> GetProtobufDataPointAttributeValues(StatsBuffer buffer)
        {
            var request = OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs)!;
            var resourceMetrics = GetLengthDelimitedFields(request, 1).Single();
            var scopeMetrics = GetLengthDelimitedFields(resourceMetrics, 2).Single();
            var metric = GetLengthDelimitedFields(scopeMetrics, 2).Single();
            var histogram = GetLengthDelimitedFields(metric, 9).Single();
            var dataPoint = GetLengthDelimitedFields(histogram, 1).Single();
            var result = new Dictionary<string, AnyValue>();

            foreach (var attribute in GetLengthDelimitedFields(dataPoint, 9))
            {
                var keyValue = KeyValue.Parser.ParseFrom(attribute);
                result[keyValue.Key] = keyValue.Value;
            }

            return result;
        }

        private static Dictionary<string, AnyValue> GetProtobufResourceAttributeValues(StatsBuffer buffer)
        {
            var request = OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs)!;
            var resourceMetrics = GetLengthDelimitedFields(request, 1).Single();
            var resource = GetLengthDelimitedFields(resourceMetrics, 1).Single();
            var result = new Dictionary<string, AnyValue>();

            foreach (var attribute in GetLengthDelimitedFields(resource, 1))
            {
                var keyValue = KeyValue.Parser.ParseFrom(attribute);
                result[keyValue.Key] = keyValue.Value;
            }

            return result;
        }

        private static List<byte[]> GetLengthDelimitedFields(byte[] message, int fieldNumber)
        {
            var fields = new List<byte[]>();
            var input = new CodedInputStream(message);

            while (true)
            {
                var tag = input.ReadTag();
                if (tag == 0)
                {
                    break;
                }

                if (WireFormat.GetTagFieldNumber(tag) == fieldNumber && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    fields.Add(input.ReadBytes().ToByteArray());
                }
                else
                {
                    input.SkipLastField();
                }
            }

            return fields;
        }

        private static string ReadAnyValue(AnyValue value)
        {
            return value.ValueCase switch
            {
                AnyValue.ValueOneofCase.StringValue => value.StringValue,
                AnyValue.ValueOneofCase.BoolValue => value.BoolValue.ToString().ToLowerInvariant(),
                AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(),
                _ => string.Empty,
            };
        }

        private static Dictionary<string, string> GetDataPointAttributes(JObject json)
        {
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]") as JObject;
            return GetDataPointAttributesFromToken(dp);
        }

        private static JObject GetJsonDataPointAttributeValue(JObject json, string key)
        {
            var attributes = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0].attributes")!;
            return (JObject)attributes.Single(attribute => attribute["key"]!.Value<string>() == key)["value"]!;
        }

        private static Dictionary<string, string> GetDataPointAttributesFromToken(JObject? dataPoint)
        {
            var result = new Dictionary<string, string>();
            var attrs = dataPoint?.SelectToken("$.attributes");
            if (attrs == null)
            {
                return result;
            }

            foreach (var attr in attrs)
            {
                var key = attr["key"]!.Value<string>()!;
                var valueNode = attr["value"]!;
                var value = valueNode["stringValue"]?.Value<string>()
                    ?? valueNode["intValue"]?.Value<string>()
                    ?? valueNode["boolValue"]?.Value<bool>().ToString().ToLowerInvariant()
                    ?? string.Empty;
                result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, string> GetResourceAttributes(JObject json)
        {
            var result = new Dictionary<string, string>();
            var attrs = json.SelectToken("$.resourceMetrics[0].resource.attributes");
            if (attrs == null)
            {
                return result;
            }

            foreach (var attr in attrs)
            {
                var key = attr["key"]!.Value<string>()!;
                var value = attr["value"]!["stringValue"]?.Value<string>() ?? string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
