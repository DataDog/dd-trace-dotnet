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

            OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs, false).Should().BeNull();
        }

        [Fact]
        public void SerializeJson_ReturnsNull_WhenNoHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 0 });

            OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs, false).Should().BeNull();
        }

        [Fact]
        public void Serialize_ReturnsNonNull_WhenHasHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 5, Duration = 100_000_000 });

            OtlpSpanStatsSerializer.Serialize(buffer, BucketDurationNs, false).Should().NotBeNull();
        }

        [Fact]
        public void SerializeJson_ReturnsNonNull_WhenHasHits()
        {
            var buffer = CreateBuffer();
            var key = CreateKey();
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 5, Duration = 100_000_000 });

            OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs, false).Should().NotBeNull();
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

        [Fact]
        public void SerializeJson_OtelSemanticsDisabled_IncludesDatadogAttributes()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(operationName: "http.request", type: "web");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer, otelSemanticsEnabled: false);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().ContainKey("datadog.operation.name");
            attrs.Should().ContainKey("datadog.span.type");
            attrs.Should().ContainKey("datadog.span.top_level");
        }

        [Fact]
        public void SerializeJson_OtelSemanticsEnabled_ExcludesDatadogAttributes()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(operationName: "http.request", type: "web");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer, otelSemanticsEnabled: true);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().NotContainKey("datadog.operation.name");
            attrs.Should().NotContainKey("datadog.span.type");
            attrs.Should().NotContainKey("datadog.span.top_level");
        }

        [Fact]
        public void SerializeJson_ServiceNameOmitted_WhenMatchesDefault()
        {
            var buffer = CreateBuffer(service: "my-service");
            var key = CreateKey(service: "my-service");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().NotContainKey("service.name");
        }

        [Fact]
        public void SerializeJson_ServiceNameIncluded_WhenDiffersFromDefault()
        {
            var buffer = CreateBuffer(service: "default-service");
            var key = CreateKey(service: "other-service");
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().ContainKey("service.name").WhoseValue.Should().Be("other-service");
        }

        [Fact]
        public void SerializeJson_ErrorDataPoint_IncludesStatusCode()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isError: true);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().ContainKey("status.code").WhoseValue.Should().Be("STATUS_CODE_ERROR");
        }

        [Fact]
        public void SerializeJson_NonErrorDataPoint_NoStatusCode()
        {
            var buffer = CreateBuffer();
            var key = CreateKey(isError: false);
            buffer.Buckets.Add(key, new StatsBucket(key, EmptyPeerTags, []) { Hits = 1, Duration = 5_000_000 });

            var json = SerializeToJson(buffer);
            var attrs = GetDataPointAttributes(json);

            attrs.Should().NotContainKey("status.code");
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
        public void SerializeJson_OtelSemanticsDisabled_ResourceIncludesRuntimeId()
        {
            var buffer = CreateBuffer();
            AddHit(buffer);

            var json = SerializeToJson(buffer, otelSemanticsEnabled: false);
            var resourceAttrs = GetResourceAttributes(json);

            resourceAttrs.Should().ContainKey("datadog.runtime_id");
        }

        [Fact]
        public void SerializeJson_OtelSemanticsEnabled_ResourceExcludesRuntimeId()
        {
            var buffer = CreateBuffer();
            AddHit(buffer);

            var json = SerializeToJson(buffer, otelSemanticsEnabled: true);
            var resourceAttrs = GetResourceAttributes(json);

            resourceAttrs.Should().NotContainKey("datadog.runtime_id");
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
            var bytes = OtlpSpanStatsSerializer.Serialize(CreateBufferWithOneHit(), BucketDurationNs, false);

            bytes.Should().NotBeNullOrEmpty();
            bytes!.Length.Should().BeGreaterThan(10);
        }

        [Fact]
        public void Serialize_Protobuf_StartsWithResourceMetricsFieldTag()
        {
            // ExportMetricsServiceRequest field 1, wire type 2 (length-delimited) → tag byte = (1 << 3) | 2 = 0x0A
            var bytes = OtlpSpanStatsSerializer.Serialize(CreateBufferWithOneHit(), BucketDurationNs, false)!;

            bytes[0].Should().Be(0x0A);
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

            var json = SerializeToJson(buffer, otelSemanticsEnabled: false);
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
            bool isTraceRoot = true,
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

        private static JObject SerializeToJson(StatsBuffer buffer, bool otelSemanticsEnabled = false)
        {
            var bytes = OtlpSpanStatsSerializer.SerializeJson(buffer, BucketDurationNs, otelSemanticsEnabled)!;
            return JObject.Parse(Encoding.UTF8.GetString(bytes));
        }

        private static Dictionary<string, string> GetDataPointAttributes(JObject json)
        {
            var dp = json.SelectToken("$.resourceMetrics[0].scopeMetrics[0].metrics[0].histogram.dataPoints[0]") as JObject;
            return GetDataPointAttributesFromToken(dp);
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
