// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.Kinesis.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
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
    public void InjectTraceIntoRecords_WithJsonString_AddsTraceContext()
    {
        var request = GeneratePutRecordsRequest(
            new List<MemoryStream>
            {
                ContextPropagation.DictionaryToMemoryStream(PersonDictionary),
                ContextPropagation.DictionaryToMemoryStream(PokemonDictionary)
            });

        var proxy = request.DuckCast<IPutRecordsRequest>();

        ContextPropagation.InjectTraceIntoRecords(proxy, _spanContext);

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can read it properly
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(datadogDictionary.ToString());

        extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
    }

    [Fact]
    public void InjectTraceIntoRecords_WithString_SkipsAddingTraceContext()
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

        ContextPropagation.InjectTraceIntoRecords(proxy, _spanContext);

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        var data = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        data.Should().Be("Jordan Gonzalez");
    }

    [Fact]
    public void InjectTraceIntoData_WithJsonString_AddsTraceContext()
    {
        var request = new PutRecordRequest
        {
            StreamName = StreamName,
            Data = ContextPropagation.DictionaryToMemoryStream(PersonDictionary)
        };

        var proxy = request.DuckCast<IPutRecordRequest>();

        ContextPropagation.InjectTraceIntoData(proxy, _spanContext);

        // Naively deserialize in order to not use tracer extraction logic
        var jsonString = Encoding.UTF8.GetString(proxy.Data.ToArray());
        var dataDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can read it properly
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(datadogDictionary.ToString());

        extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
    }

    [Fact]
    public void InjectTraceIntoData_WithLargeJsonString_SkipsAddingTraceContext()
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

        ContextPropagation.InjectTraceIntoData(proxy, _spanContext);

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
}
