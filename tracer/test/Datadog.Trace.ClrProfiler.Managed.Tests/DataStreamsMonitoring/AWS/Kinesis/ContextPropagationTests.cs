// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Kinesis.Model;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Kinesis;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";
    private const string StreamName = "MyStreamName";

    private static readonly Dictionary<string, object> PersonDictionary = new() { { "name", "Jordan" }, { "lastname", "Gonzalez" }, { "city", "NYC" }, { "age", 24 } };
    private static readonly Dictionary<string, object> PokemonDictionary = new() { { "id", 393 }, { "name", "Piplup" }, { "type", "water" } };
    private static readonly byte[] PersonJsonStringBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(PersonDictionary));
    private static readonly byte[] StreamNameBytes = Encoding.UTF8.GetBytes(StreamName);

    private readonly SpanContext _spanContext;

    public ContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 1, "test-kinesis", "serverless");
    }

    public static IEnumerable<object[]> MemoryStreamToDictionaryExpectedData
        => new List<object[]>
        {
            new object[] { PersonJsonStringBytes, PersonDictionary },
            new object[] { StreamNameBytes, null },
        };

    public PutRecordsRequest GeneratePutRecordsRequest(List<MemoryStream> records)
    {
        var request = new PutRecordsRequest
        {
            StreamName = StreamName,
            Records = new List<PutRecordsRequestEntry>()
        };

        foreach (var record in records)
        {
            var entry = new PutRecordsRequestEntry { Data = record, PartitionKey = Guid.NewGuid().ToString() };
            request.Records.Add(entry);
        }

        return request;
    }

    [Fact]
    public async Task InjectTraceIntoData_WithAwsSdkDisabled_SkipsAddingTraceContext()
    {
        var request = GeneratePutRecordsRequest(
            new List<MemoryStream>
            {
                ContextPropagation.DictionaryToMemoryStream(PersonDictionary),
                ContextPropagation.DictionaryToMemoryStream(PokemonDictionary)
            });

        var proxy = request.DuckCast<IPutRecordsRequest>();

        await using var tracer = GetAwsSdkDisabledTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecords", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoRecords(tracer, proxy, scope, "streamname");

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeFalse();
    }

    [Fact]
    public async Task InjectTraceIntoData_WithAwsKinesisDisabled_SkipsAddingTraceContext()
    {
        var request = GeneratePutRecordsRequest(
            new List<MemoryStream>
            {
                ContextPropagation.DictionaryToMemoryStream(PersonDictionary),
                ContextPropagation.DictionaryToMemoryStream(PokemonDictionary)
            });

        var proxy = request.DuckCast<IPutRecordsRequest>();

        await using var tracer = GetAwsKinesisDisabledTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecords", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoRecords(tracer, proxy, scope, "streamname");

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeFalse();
    }

    [Fact]
    public async Task InjectTraceIntoRecords_WithJsonString_AddsTraceContext()
    {
        var request = GeneratePutRecordsRequest(
            new List<MemoryStream>
            {
                ContextPropagation.DictionaryToMemoryStream(PersonDictionary),
                ContextPropagation.DictionaryToMemoryStream(PokemonDictionary)
            });

        var proxy = request.DuckCast<IPutRecordsRequest>();

        await using var tracer = GetTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecords", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoRecords(tracer, proxy, scope, "streamname");

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, object> so we can read it properly
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(datadogDictionary?.ToString() ?? string.Empty);

        extractedTraceContext["x-datadog-parent-id"].Should().Be(scope?.Span.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(scope?.Span.TraceId.ToString());
        extractedTraceContext["dd-pathway-ctx-base64"].As<Newtonsoft.Json.Linq.JArray>().Should().HaveCount(1);
    }

    [Fact]
    public async Task InjectTraceIntoRecords_WithString_SkipsAddingTraceContext()
    {
        const string person = "Jordan Gonzalez";
        const string pokemon = "Piplup";
        var request = GeneratePutRecordsRequest(
            new List<MemoryStream>
            {
                new(Encoding.UTF8.GetBytes(person)),
                new(Encoding.UTF8.GetBytes(pokemon))
            });

        var proxy = request.DuckCast<IPutRecordsRequest>();

        await using var tracer = GetTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecords", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoRecords(tracer, proxy, scope, "streamname");

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        var data = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        data.Should().Be("Jordan Gonzalez");
    }

    [Fact]
    public async Task InjectTraceIntoData_WithJsonString_AddsTraceContext()
    {
        var request = new PutRecordRequest
        {
            StreamName = StreamName,
            Data = ContextPropagation.DictionaryToMemoryStream(PersonDictionary)
        };

        var proxy = request.DuckCast<IPutRecordRequest>();

        await using var tracer = GetTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecord", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoData(tracer, proxy, scope, "streamname");

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(proxy.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, object> so we can read it properly
        datadogDictionary.Should().NotBeNull();
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(datadogDictionary?.ToString() ?? string.Empty);

        extractedTraceContext["x-datadog-parent-id"].Should().Be(scope?.Span.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(scope?.Span.TraceId.ToString());
        extractedTraceContext["dd-pathway-ctx-base64"].As<Newtonsoft.Json.Linq.JArray>().Should().HaveCount(1);
    }

    [Fact]
    public async Task InjectTraceIntoData_WithLargeJsonString_SkipsAddingTraceContext()
    {
        var largeDictionary = new Dictionary<string, object>
        {
            { "person", PersonDictionary },
            { "blob", new string('x', 1024 * 1024) }
        };
        var request = new PutRecordRequest
        {
            StreamName = StreamName,
            Data = ContextPropagation.DictionaryToMemoryStream(largeDictionary)
        };

        var proxy = request.DuckCast<IPutRecordRequest>();

        await using var tracer = GetTracer();
        var scope = AwsKinesisCommon.CreateScope(tracer, "PutRecord", SpanKinds.Producer, null, out var tags);
        ContextPropagation.InjectTraceIntoData(tracer, proxy, scope, "streamname");

        var data = proxy.Data;

        // Length has not changed, therefore, no trace was injected.
        data.Length.Should().Be(request.Data.Length);
        data.Should().BeSameAs(request.Data);
    }

    [Theory]
    [MemberData(nameof(MemoryStreamToDictionaryExpectedData))]
    public void ParseDataObject_ReturnsExpectedValue(byte[] bytes, Dictionary<string, object> expected)
    {
        var memoryStream = new MemoryStream(bytes);
        var result = ContextPropagation.ParseDataObject(memoryStream);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MemoryStreamToDictionary_WithJsonString_ReturnsDictionary()
    {
        // JSON string
        var personMemoryStream = new MemoryStream(PersonJsonStringBytes);
        var personDictionary = ContextPropagation.MemoryStreamToDictionary(personMemoryStream);
        personDictionary.Should().BeEquivalentTo(PersonDictionary);
    }

    [Fact]
    public void MemoryStreamToDictionary_WithNonJsonString_ThrowsException()
    {
        // Anything that is not a JSON string, will throw an error
        var streamNameMemoryStream = new MemoryStream(StreamNameBytes);
        Assert.ThrowsAny<Exception>(() => ContextPropagation.MemoryStreamToDictionary(streamNameMemoryStream));
    }

    [Fact]
    public void DictionaryToMemoryStream_ReturnsMemoryStream()
    {
        var personMemoryStream = ContextPropagation.DictionaryToMemoryStream(PersonDictionary);
        personMemoryStream.Should().NotBeNull();

        personMemoryStream.ToArray().Should().BeEquivalentTo(PersonJsonStringBytes);
    }

    [Fact]
    public void ExtractPathwayContext_WithInjectedArrayValue_ReturnsContext()
    {
        var dsm = GetDataStreamsManager();
        var context = new PathwayContext(new PathwayHash(123), 1_234_000_000, 5_678_000_000);
        var adapter = new KinesisContextAdapter();
        dsm.InjectPathwayContext(context, adapter);

        var data = new Dictionary<string, object>(PersonDictionary) { [DatadogKey] = adapter.GetDictionary() };
        var stream = ContextPropagation.DictionaryToMemoryStream(data);

        var extracted = ContextPropagation.ExtractPathwayContext(dsm, stream);

        extracted.Should().NotBeNull();
        extracted!.Value.Hash.Value.Should().Be(context.Hash.Value);
        extracted.Value.PathwayStart.Should().Be(context.PathwayStart);
        extracted.Value.EdgeStart.Should().Be(context.EdgeStart);
    }

    [Fact]
    public void ExtractPathwayContext_WithStringValue_ReturnsContext()
    {
        var dsm = GetDataStreamsManager();
        var context = new PathwayContext(new PathwayHash(456), 1_234_000_000, 5_678_000_000);
        var adapter = new KinesisContextAdapter();
        dsm.InjectPathwayContext(context, adapter);
        var encoded = adapter.GetValues("dd-pathway-ctx-base64").Should().ContainSingle().Subject;

        var json = $"{{\"before\":{{\"nested\":[1,2]}},\"{DatadogKey}\":{{\"x-datadog-trace-id\":\"1\",\"dd-pathway-ctx-base64\":\"{encoded}\"}},\"after\":true}}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var extracted = ContextPropagation.ExtractPathwayContext(dsm, stream);

        extracted.Should().NotBeNull();
        extracted!.Value.Hash.Value.Should().Be(context.Hash.Value);
    }

    [Fact]
    public void ExtractPathwayContext_PreservesStreamPosition()
    {
        var dsm = GetDataStreamsManager();
        var adapter = new KinesisContextAdapter();
        dsm.InjectPathwayContext(new PathwayContext(new PathwayHash(789), 1, 2), adapter);
        var data = new Dictionary<string, object>(PersonDictionary) { [DatadogKey] = adapter.GetDictionary() };
        var stream = ContextPropagation.DictionaryToMemoryStream(data);
        stream.Position = 7;

        ContextPropagation.ExtractPathwayContext(dsm, stream);

        stream.Position.Should().Be(7);
    }

    [Theory]
    [InlineData("Jordan Gonzalez is not json at all")]
    [InlineData("[\"an\", \"array\", \"of\", \"strings\", \"long enough\"]")]
    [InlineData("{\"name\":\"Jordan\",\"lastname\":\"Gonzalez\",\"city\":\"NYC\"}")]
    [InlineData("{\"name\":\"Jordan\",\"_datadog\":\"not an object at all\"}")]
    [InlineData("{\"name\":\"Jordan\",\"_datadog\":{\"dd-pathway-ctx-base64\":\"!!notbase64!!\"}}")]
    public void ExtractPathwayContext_WithoutValidContext_ReturnsNull(string data)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));

        ContextPropagation.ExtractPathwayContext(GetDataStreamsManager(), stream).Should().BeNull();
    }

    private static DataStreamsManager GetDataStreamsManager()
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" } });
        return new DataStreamsManager(settings, Mock.Of<IDataStreamsWriter>(), Mock.Of<IDiscoveryService>());
    }

    private static ScopedTracer GetTracer(string schemaVersion = "v1")
    {
        var settings = TracerSettings.Create(new() { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } });
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }

    private static ScopedTracer GetAwsSdkDisabledTracer(string schemaVersion = "v1")
    {
        var settings = TracerSettings.Create(new() { { "DD_TRACE_AwsSdk_ENABLED", "false" } });
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }

    private static ScopedTracer GetAwsKinesisDisabledTracer(string schemaVersion = "v1")
    {
        var settings = TracerSettings.Create(new() { { "DD_TRACE_AwsKinesis_ENABLED", "false" } });
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }
}
