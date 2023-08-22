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
using Datadog.Trace.Propagators;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";
    private const string StreamName = "MyStreamName";

    private static readonly Dictionary<string, object> PersonDictionary = new() { { "name", "Jordan" }, { "lastname", "Gonzalez" }, { "city", "NYC" } };
    private static readonly Dictionary<string, object> PokemonDictionary = new() { { "id", 393 }, { "name", "Piplup" }, { "type", "water" } };
    private static readonly byte[] PersonJsonStringBytes = Encoding.UTF8.GetBytes(Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(PersonDictionary));
    private static readonly string PersonBase64String = Convert.ToBase64String(PersonJsonStringBytes);
    private static readonly MemoryStream PersonMemoryStream = new(PersonJsonStringBytes);
    private static readonly MemoryStream EncodedPersonMemoryStream = new(Convert.FromBase64String(PersonBase64String));
    private static readonly MemoryStream StreamNameMemoryStream = new(Encoding.UTF8.GetBytes(StreamName));

    private readonly SpanContext spanContext;

    public ContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        ulong spanId = 6766950223540265769;
        spanContext = new SpanContext(traceId, spanId, 1, "test-kinesis", "serverless");
    }

    public static IEnumerable<object[]> MemoryStreamToDictionaryExpectedData
        => new List<object[]>
        {
            new object[] { EncodedPersonMemoryStream, PersonDictionary },
            new object[] { PersonMemoryStream, PersonDictionary },
            new object[] { StreamNameMemoryStream, null },
        };

    [Fact]
    public void InjectTraceIntoRecords_WithJsonString_AddsTraceContext()
    {
        var request = new PutRecordsRequest { StreamName = StreamName, Records = new List<PutRecordsRequestEntry> { new PutRecordsRequestEntry { Data = ContextPropagation.DictionaryToMemoryStream(PersonDictionary), PartitionKey = Guid.NewGuid().ToString() }, new PutRecordsRequestEntry { Data = ContextPropagation.DictionaryToMemoryStream(PokemonDictionary), PartitionKey = Guid.NewGuid().ToString() } } };

        var proxy = request.DuckCast<IPutRecordsRequest>();

        ContextPropagation.InjectTraceIntoRecords<PutRecordsRequest>(proxy, spanContext);

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        // MemoryStreamToDictionary returns a Dictionary<string, object>
        var dataDictionary = ContextPropagation.MemoryStreamToDictionary(firstRecord.Data);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can extract it properly
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(datadogDictionary.ToString());

        var extractedSpanContext = SpanContextPropagator.Instance.Extract(dictionary);
        extractedSpanContext.Should().NotBeNull();
        extractedSpanContext.TraceId.Should().Be(spanContext.TraceId);
        extractedSpanContext.SpanId.Should().Be(spanContext.SpanId);
    }

    [Fact]
    public void InjectTraceIntoRecords_WithString_SkipsAddingTraceContext()
    {
        const string person = "Jordan Gonzalez";
        const string pokemon = "Piplup";
        var request = new PutRecordsRequest { StreamName = StreamName, Records = new List<PutRecordsRequestEntry> { new PutRecordsRequestEntry { Data = new MemoryStream(Encoding.UTF8.GetBytes(person)), PartitionKey = Guid.NewGuid().ToString() }, new PutRecordsRequestEntry { Data = new MemoryStream(Encoding.UTF8.GetBytes(pokemon)), PartitionKey = Guid.NewGuid().ToString() } } };

        var proxy = request.DuckCast<IPutRecordsRequest>();

        ContextPropagation.InjectTraceIntoRecords<PutRecordsRequest>(proxy, spanContext);

        var firstRecord = proxy.Records[0].DuckCast<IContainsData>();

        var data = Encoding.UTF8.GetString(firstRecord.Data.ToArray());
        data.Should().Be("Jordan Gonzalez");
    }

    [Fact]
    public void InjectTraceIntoData_WithJsonString_AddsTraceContext()
    {
        var request = new PutRecordRequest { StreamName = StreamName, Data = ContextPropagation.DictionaryToMemoryStream(PersonDictionary) };

        var proxy = request.DuckCast<IPutRecordRequest>();

        ContextPropagation.InjectTraceIntoData<PutRecordsRequest>(proxy, spanContext);

        // MemoryStreamToDictionary returns a Dictionary<string, object>
        var dataDictionary = ContextPropagation.MemoryStreamToDictionary(proxy.Data);
        var extracted = dataDictionary.TryGetValue(DatadogKey, out var datadogDictionary);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can extract it properly
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(datadogDictionary.ToString());

        var extractedSpanContext = SpanContextPropagator.Instance.Extract(dictionary);
        extractedSpanContext.Should().NotBeNull();
        extractedSpanContext.TraceId.Should().Be(spanContext.TraceId);
        extractedSpanContext.SpanId.Should().Be(spanContext.SpanId);
    }

    [Theory]
    [MemberData(nameof(MemoryStreamToDictionaryExpectedData))]
    public void ParseDataObject_ReturnsExpectedValue(MemoryStream memoryStream, Dictionary<string, object> expected)
    {
        var result = ContextPropagation.ParseDataObject(memoryStream);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MemoryStreamToDictionary_WithJsonString_ReturnsDictionary()
    {
        // JsonString
        var personDictionary = ContextPropagation.MemoryStreamToDictionary(PersonMemoryStream);
        personDictionary.Should().BeEquivalentTo(PersonDictionary);

        // Base64 JsonString
        personDictionary = ContextPropagation.MemoryStreamToDictionary(EncodedPersonMemoryStream);
        personDictionary.Should().BeEquivalentTo(PersonDictionary);
    }

    [Fact]
    public void MemoryStreamToDictionary_WithNonJsonString_ThrowsException()
    {
        Assert.ThrowsAny<Exception>(() => ContextPropagation.MemoryStreamToDictionary(StreamNameMemoryStream));
    }
}
