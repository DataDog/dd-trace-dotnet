// <copyright file="MockOtlpTraceDecodingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.MockOtlp;
using FluentAssertions;
using Google.Protobuf;
using MessagePack;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using Xunit;
using Xunit.Abstractions;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpStatus = OpenTelemetry.Proto.Trace.V1.Status;
using OtlpStatusCode = OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode;

namespace Datadog.Trace.Tests.Agent;

public class MockOtlpTraceDecodingTests
{
    private const string TraceIdHex = "0102030405060708090a0b0c0d0e0f10";
    private const string SpanIdHex = "1122334455667788";
    private const string ParentSpanIdHex = "0102030405060708";

    private readonly ITestOutputHelper _output;

    public MockOtlpTraceDecodingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ProtobufDecode_ProducesTypedSpan()
    {
        using var agent = MockTracerAgent.Create(_output);
        var request = CreateExportRequest();

        var response = await PostAsync(agent, "/v1/traces", request.ToByteArray(), "application/x-protobuf");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-protobuf");
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();

        var span = agent.OtlpSpans.Should().ContainSingle().Subject;
        AssertDecodedSpan(span);
    }

    [Fact]
    public async Task JsonDecode_WithHexIds_ProducesSameSpanAsProtobuf()
    {
        using var agent = MockTracerAgent.Create(_output);
        var json = CreateJsonExportRequest();

        var response = await PostAsync(agent, "/v1/traces", Encoding.UTF8.GetBytes(json), "application/json");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Be("{}");

        var span = agent.OtlpSpans.Should().ContainSingle().Subject;
        AssertDecodedSpan(span);
    }

    [Fact]
    public async Task GzipCompressedBody_DecodesCorrectly()
    {
        using var agent = MockTracerAgent.Create(_output);
        var request = CreateExportRequest();

        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = request.ToByteArray();
            gzip.Write(bytes, 0, bytes.Length);
        }

        var response = await PostAsync(agent, "/v1/traces", compressed.ToArray(), "application/x-protobuf", gzip: true);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        agent.OtlpSpans.Should().ContainSingle();
    }

    [Fact]
    public async Task UnsupportedContentType_ReturnsClearError()
    {
        using var agent = MockTracerAgent.Create(_output);

        var response = await PostAsync(agent, "/v1/traces", Encoding.UTF8.GetBytes("not otlp"), "text/plain");

        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400);
        agent.OtlpSpans.Should().BeEmpty();
    }

    [Fact]
    public async Task MetricsAndLogs_AreCapturedRaw_AndDoNotReachTheTraceDecoders()
    {
        using var agent = MockTracerAgent.Create(_output);
        var metricsBody = Encoding.UTF8.GetBytes("fake-metrics-payload");
        var logsBody = Encoding.UTF8.GetBytes("fake-logs-payload");

        await PostAsync(agent, "/v1/metrics", metricsBody, "application/x-protobuf");
        await PostAsync(agent, "/v1/logs", logsBody, "application/x-protobuf");

        agent.OtlpMetricsRequests.Should().ContainSingle().Which.Body.Should().Equal(metricsBody);
        agent.OtlpLogsRequests.Should().ContainSingle().Which.Body.Should().Equal(logsBody);
        agent.OtlpSpans.Should().BeEmpty();
        agent.Spans.Should().BeEmpty();
    }

    [Fact]
    public async Task DatadogAndOtlpTraceRequests_CoexistOnTheSameAgent()
    {
        using var agent = MockTracerAgent.Create(_output);

        var datadogSpans = new[] { new[] { new MockSpan { TraceId = 1, SpanId = 1, Name = "datadog.span" } } };
        var msgPackBody = MessagePackSerializer.Serialize(datadogSpans);
        using var client = new HttpClient();
        using var msgPackRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{Port(agent)}/v0.4/traces")
        {
            Content = new ByteArrayContent(msgPackBody),
        };
        msgPackRequest.Content.Headers.Add("Content-Type", "application/msgpack");
        msgPackRequest.Headers.Add("X-Datadog-Trace-Count", "1");
        await client.SendAsync(msgPackRequest);

        await PostAsync(agent, "/v1/traces", CreateExportRequest().ToByteArray(), "application/x-protobuf");

        agent.Spans.Should().ContainSingle(s => s.Name == "datadog.span");
        agent.OtlpSpans.Should().ContainSingle();
    }

    [Fact]
    public async Task WaitForOtlpSpansAsync_ReturnsSpans_AndAssertsContentTypeHeader()
    {
        using var agent = MockTracerAgent.Create(_output);

        await PostAsync(agent, "/v1/traces", CreateExportRequest().ToByteArray(), "application/x-protobuf");

        var spans = await agent.WaitForOtlpSpansAsync(count: 1, timeoutInMilliseconds: 5000);
        spans.Should().ContainSingle();
    }

    private static int Port(MockTracerAgent agent) => ((MockTracerAgent.TcpUdpAgent)agent).Port;

    private static async Task<HttpResponseMessage> PostAsync(MockTracerAgent agent, string path, byte[] body, string contentType, bool gzip = false)
    {
        using var client = new HttpClient();
        using var content = new ByteArrayContent(body);
        content.Headers.Add("Content-Type", contentType);
        if (gzip)
        {
            content.Headers.Add("Content-Encoding", "gzip");
        }

        return await client.PostAsync($"http://127.0.0.1:{Port(agent)}{path}", content);
    }

    private static ExportTraceServiceRequest CreateExportRequest()
    {
        var span = new OtlpSpan
        {
            TraceId = ByteStringFromHex(TraceIdHex),
            SpanId = ByteStringFromHex(SpanIdHex),
            ParentSpanId = ByteStringFromHex(ParentSpanIdHex),
            TraceState = "vendor=value",
            Flags = 1,
            Name = "test.operation",
            Kind = OtlpSpan.Types.SpanKind.Server,
            StartTimeUnixNano = 1_000_000_000UL,
            EndTimeUnixNano = 2_000_000_000UL,
        };
        span.Attributes.Add(new KeyValue { Key = "http.method", Value = new AnyValue { StringValue = "GET" } });
        span.Attributes.Add(new KeyValue { Key = "http.status_code", Value = new AnyValue { IntValue = 200 } });
        span.Events.Add(new OtlpSpan.Types.Event { Name = "exception", TimeUnixNano = 1_500_000_000UL });
        span.Events[0].Attributes.Add(new KeyValue { Key = "exception.message", Value = new AnyValue { StringValue = "boom" } });
        span.Links.Add(new OtlpSpan.Types.Link { TraceId = ByteStringFromHex(TraceIdHex), SpanId = ByteStringFromHex(SpanIdHex) });
        span.Status = new OtlpStatus { Code = OtlpStatusCode.Ok, Message = "ok" };

        var scopeSpans = new OtlpScopeSpans
        {
            Scope = new InstrumentationScope { Name = "test-scope", Version = "1.0.0" },
        };
        scopeSpans.Spans.Add(span);

        var resourceSpans = new OtlpResourceSpans
        {
            Resource = new Resource(),
        };
        resourceSpans.Resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "test-service" } });
        resourceSpans.ScopeSpans.Add(scopeSpans);

        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resourceSpans);
        return request;
    }

    private static string CreateJsonExportRequest()
    {
        var span = new JObject
        {
            ["traceId"] = TraceIdHex,
            ["spanId"] = SpanIdHex,
            ["parentSpanId"] = ParentSpanIdHex,
            ["traceState"] = "vendor=value",
            ["flags"] = 1,
            ["name"] = "test.operation",
            ["kind"] = (int)OtlpSpan.Types.SpanKind.Server,
            ["startTimeUnixNano"] = "1000000000",
            ["endTimeUnixNano"] = "2000000000",
            ["attributes"] = new JArray
            {
                new JObject { ["key"] = "http.method", ["value"] = new JObject { ["stringValue"] = "GET" } },
                new JObject { ["key"] = "http.status_code", ["value"] = new JObject { ["intValue"] = "200" } },
            },
            ["events"] = new JArray
            {
                new JObject
                {
                    ["name"] = "exception",
                    ["timeUnixNano"] = "1500000000",
                    ["attributes"] = new JArray
                    {
                        new JObject { ["key"] = "exception.message", ["value"] = new JObject { ["stringValue"] = "boom" } },
                    },
                },
            },
            ["links"] = new JArray
            {
                new JObject { ["traceId"] = TraceIdHex, ["spanId"] = SpanIdHex },
            },
            ["status"] = new JObject { ["code"] = (int)OtlpStatusCode.Ok, ["message"] = "ok" },
        };

        var root = new JObject
        {
            ["resourceSpans"] = new JArray
            {
                new JObject
                {
                    ["resource"] = new JObject
                    {
                        ["attributes"] = new JArray
                        {
                            new JObject { ["key"] = "service.name", ["value"] = new JObject { ["stringValue"] = "test-service" } },
                        },
                    },
                    ["scopeSpans"] = new JArray
                    {
                        new JObject
                        {
                            ["scope"] = new JObject { ["name"] = "test-scope", ["version"] = "1.0.0" },
                            ["spans"] = new JArray { span },
                        },
                    },
                },
            },
        };

        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static void AssertDecodedSpan(MockOtlpSpan span)
    {
        span.TraceId.Should().Be(TraceIdHex);
        span.SpanId.Should().Be(SpanIdHex);
        span.ParentSpanId.Should().Be(ParentSpanIdHex);
        span.TraceState.Should().Be("vendor=value");
        span.Name.Should().Be("test.operation");
        span.Kind.Should().Be(OtlpSpan.Types.SpanKind.Server);
        span.StartTimeUnixNano.Should().Be(1_000_000_000UL);
        span.EndTimeUnixNano.Should().Be(2_000_000_000UL);

        var methodAttribute = span.Attributes.Should().ContainSingle(a => a.Key == "http.method").Subject;
        methodAttribute.Value.Kind.Should().Be(MockOtlpAttributeValueKind.String);
        methodAttribute.Value.StringValue.Should().Be("GET");

        var statusCodeAttribute = span.Attributes.Should().ContainSingle(a => a.Key == "http.status_code").Subject;
        statusCodeAttribute.Value.Kind.Should().Be(MockOtlpAttributeValueKind.Int);
        statusCodeAttribute.Value.IntValue.Should().Be(200);

        var otlpEvent = span.Events.Should().ContainSingle().Subject;
        otlpEvent.Name.Should().Be("exception");
        otlpEvent.Attributes.Should().ContainSingle(a => a.Key == "exception.message" && a.Value.StringValue == "boom");

        var link = span.Links.Should().ContainSingle().Subject;
        link.TraceId.Should().Be(TraceIdHex);
        link.SpanId.Should().Be(SpanIdHex);

        span.Status.Should().NotBeNull();
        span.Status!.Code.Should().Be(OtlpStatusCode.Ok);
        span.Status.Message.Should().Be("ok");
    }

    private static ByteString ByteStringFromHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return ByteString.CopyFrom(bytes);
    }
}
