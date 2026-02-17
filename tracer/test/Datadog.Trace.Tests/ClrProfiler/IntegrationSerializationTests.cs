// <copyright file="IntegrationSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler;

/// <summary>
/// Baseline serialization tests for ClrProfiler auto-instrumentation JSON patterns.
/// Covers: AWS Kinesis ContextPropagation (streaming Dictionary), AWS SQS/SNS message
/// attributes (Dictionary deserialization), Process (Collection serialization),
/// Selenium (Dictionary serialization), Azure Functions (generic deserialization).
/// </summary>
public class IntegrationSerializationTests
{
    // ===== Pattern: AWS Kinesis ContextPropagation.cs — streaming Dictionary<string, object> =====

    [Fact]
    public void KinesisContextPropagation_MemoryStreamToDictionary_StreamingDeserialize()
    {
        // Exact pattern from ContextPropagation.cs line 126-137:
        // var streamReader = new StreamReader(stream);
        // var reader = new JsonTextReader(streamReader);
        // var serializer = new JsonSerializer();
        // return serializer.Deserialize<Dictionary<string, object>>(reader);
        // language=json
        var json = """{"key1":"value1","key2":42,"key3":true}""";
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var streamReader = new StreamReader(ms);
        var reader = new JsonTextReader(streamReader);
        var serializer = new JsonSerializer();

        var result = serializer.Deserialize<Dictionary<string, object>>(reader);

        result.Should().NotBeNull();
        result!["key1"].ToString().Should().Be("value1");
        result["key2"].Should().BeOfType<long>().Which.Should().Be(42L); // Newtonsoft deserializes int → long
        result["key3"].Should().BeOfType<bool>().Which.Should().Be(true);
    }

    [Fact]
    public void KinesisContextPropagation_DictionaryToMemoryStream_StreamingSerialize()
    {
        // Exact pattern from ContextPropagation.cs line 139-150:
        // var writer = new StreamWriter(memoryStream);
        // var serializer = new JsonSerializer();
        // serializer.Serialize(writer, dictionary);
        var dictionary = new Dictionary<string, object>
        {
            { "message", "hello" },
            { "_datadog", new Dictionary<string, object> { { "x-datadog-trace-id", "12345" } } },
        };

        using var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        var serializer = new JsonSerializer();
        serializer.Serialize(writer, dictionary);
        writer.Flush();

        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();
        json.Should().Contain("\"message\":\"hello\"");
        json.Should().Contain("\"_datadog\":");
        json.Should().Contain("\"x-datadog-trace-id\":\"12345\"");
    }

    [Fact]
    public void KinesisContextPropagation_RoundTrip_StreamingDictionary()
    {
        // Full round-trip: Dictionary → MemoryStream → Dictionary
        var original = new Dictionary<string, object>
        {
            { "data", "payload" },
            { "_datadog", new Dictionary<string, object> { { "trace-id", "abc" } } },
        };

        // Serialize
        using var ms = new MemoryStream();
        var serializer = new JsonSerializer();
        using (var writer = new StreamWriter(ms, leaveOpen: true))
        {
            serializer.Serialize(writer, original);
        }

        // Deserialize
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        using var jsonReader = new JsonTextReader(reader);
        var result = serializer.Deserialize<Dictionary<string, object>>(jsonReader);

        result!["data"].ToString().Should().Be("payload");
    }

    // ===== Pattern: AWS AwsMessageAttributesHeadersAdapters.cs — DeserializeObject<Dictionary<string, string>> =====

    [Fact]
    public void AwsMessageAttributes_DeserializeObject_DictionaryStringString()
    {
        // Exact pattern from AwsMessageAttributesHeadersAdapters.cs line 98:
        // JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString)
        // language=json
        var jsonString = """{"x-datadog-trace-id":"12345678","x-datadog-parent-id":"87654321","x-datadog-sampling-priority":"1"}""";

        var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString)!;

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result["x-datadog-trace-id"].Should().Be("12345678");
        result["x-datadog-parent-id"].Should().Be("87654321");
        result["x-datadog-sampling-priority"].Should().Be("1");
    }

    // ===== Pattern: Process/ProcessStartCommon.cs — SerializeObject Collection<string> and string[] =====

    [Fact]
    public void ProcessStartCommon_SerializeObject_CollectionString()
    {
        // Exact pattern from ProcessStartCommon.cs line 178/204:
        // tags.CommandExec = JsonConvert.SerializeObject(finalCommandExec);
        // where finalCommandExec is Collection<string>
        var finalCommandExec = new Collection<string> { "/usr/bin/python", "script.py", "--verbose" };

        var json = JsonConvert.SerializeObject(finalCommandExec);
        json.Should().Be("""["/usr/bin/python","script.py","--verbose"]""");
    }

    [Fact]
    public void ProcessStartCommon_SerializeObject_SingleFilenameArray()
    {
        // Exact pattern from ProcessStartCommon.cs line 208:
        // tags.CommandExec = JsonConvert.SerializeObject(new[] { filename });
        var filename = "/usr/bin/ls";
        var json = JsonConvert.SerializeObject(new[] { filename });
        json.Should().Be("""["/usr/bin/ls"]""");
    }

    // ===== Pattern: Testing/Selenium/SeleniumCommon.cs — SerializeObject Dictionary<string, object>? =====

    [Fact]
    public void SeleniumCommon_SerializeObject_DictionaryStringObject()
    {
        // Exact pattern from SeleniumCommon.cs line 84:
        // JsonConvert.SerializeObject(parameters ?? new object())
        var parameters = new Dictionary<string, object>
        {
            { "url", "https://example.com" },
            { "timeout", 30 },
            { "headless", true },
        };

        var json = JsonConvert.SerializeObject(parameters ?? new object());
        json.Should().Contain("\"url\":\"https://example.com\"");
        json.Should().Contain("\"timeout\":30");
        json.Should().Contain("\"headless\":true");
    }

    [Fact]
    public void SeleniumCommon_SerializeObject_NullParametersFallback()
    {
        // When parameters is null, serializes empty object
        Dictionary<string, object>? parameters = null;
        var json = JsonConvert.SerializeObject(parameters ?? new object());
        json.Should().Be("{}");
    }

    // ===== Pattern: Azure/Functions/AzureFunctionsCommon.cs — DeserializeObject<T> generic =====

    [Fact]
    public void AzureFunctionsCommon_DeserializeObject_GenericType()
    {
        // Exact pattern from AzureFunctionsCommon.cs line 438:
        // JsonConvert.DeserializeObject<T>(jsonString)
        // Used for parsing service bus, event hub, and other binding data
        // language=json
        var json = """{"connectionString":"Endpoint=sb://test.servicebus.windows.net/","entityPath":"myqueue"}""";

        var result = JsonConvert.DeserializeObject<ServiceBusPropsTestModel>(json);
        result.Should().NotBeNull();
        result!.ConnectionString.Should().Be("Endpoint=sb://test.servicebus.windows.net/");
        result.EntityPath.Should().Be("myqueue");
    }

    // ===== Test models =====

    private sealed class ServiceBusPropsTestModel
    {
        [JsonProperty("connectionString")]
        public string ConnectionString { get; set; } = null!;

        [JsonProperty("entityPath")]
        public string EntityPath { get; set; } = null!;
    }
}
