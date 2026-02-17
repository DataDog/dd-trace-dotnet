// <copyright file="AgentSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent;

/// <summary>
/// Baseline serialization tests for Agent transport JSON patterns.
/// Covers: Api.ApiResponse deserialization, IApiResponse.ReadAsType streaming deserialization,
/// SerializationHelpers.WriteAsJson streaming serialization, and SpanEventConverter.
/// </summary>
public class AgentSerializationTests
{
    // ===== Pattern: Agent/Api.cs — DeserializeObject<ApiResponse> =====
    // ApiResponse is an internal struct with [JsonProperty("rate_by_service")] on Dictionary<string, float>

    [Fact]
    public void ApiResponse_DeserializeObject_ParsesRateByService()
    {
        // Exact pattern from Api.cs line 371:
        // var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent);
        // language=json
        var json = """{"rate_by_service":{"service:my-svc,env:prod":0.5,"service:other,env:dev":1.0}}""";

        var result = JsonConvert.DeserializeObject<ApiResponseTestModel>(json);

        result.RateByService.Should().NotBeNull();
        result.RateByService.Should().HaveCount(2);
        result.RateByService["service:my-svc,env:prod"].Should().Be(0.5f);
        result.RateByService["service:other,env:dev"].Should().Be(1.0f);
    }

    [Fact]
    public void ApiResponse_DeserializeObject_EmptyRateByService()
    {
        // language=json
        var json = """{"rate_by_service":{}}""";
        var result = JsonConvert.DeserializeObject<ApiResponseTestModel>(json);
        result.RateByService.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ApiResponse_DeserializeObject_MissingRateByService()
    {
        // language=json
        var json = """{}""";
        var result = JsonConvert.DeserializeObject<ApiResponseTestModel>(json);
        result.RateByService.Should().BeNull();
    }

    // ===== Pattern: IApiResponse.ReadAsType<T> — JsonSerializer.Create().Deserialize<T>(jsonTextReader) =====
    // Uses default settings (no snake_case, no NullValueHandling.Ignore)

    [Fact]
    public void ReadAsType_Pattern_DeserializesViaStreamingReader()
    {
        // Exact pattern from IApiResponse.cs line 69-70:
        // using var jsonTextReader = new JsonTextReader(sr);
        // return JsonSerializer.Create().Deserialize<T>(jsonTextReader);
        // language=json
        var json = """{"rate_by_service":{"service:web,env:prod":0.75}}""";

        using var sr = new StringReader(json);
        using var jsonTextReader = new JsonTextReader(sr);
        var result = JsonSerializer.Create().Deserialize<ApiResponseTestModel>(jsonTextReader);

        result.Should().NotBeNull();
        result.RateByService.Should().ContainKey("service:web,env:prod").WhoseValue.Should().Be(0.75f);
    }

    // ===== Pattern: SerializationHelpers.DefaultJsonSettings =====
    // NullValueHandling.Ignore + SnakeCaseNamingStrategy

    [Fact]
    public void DefaultJsonSettings_SnakeCase_SerializesPropertyNames()
    {
        // Exact settings from SerializationHelpers.cs line 19-26
        var settings = SerializationHelpers.DefaultJsonSettings;
        var payload = new SamplePayload { MyProperty = "hello", NestedValue = 42, NullableField = null };

        var json = JsonConvert.SerializeObject(payload, settings);

        json.Should().Contain("\"my_property\"");
        json.Should().Contain("\"nested_value\"");
        // NullValueHandling.Ignore should omit null fields
        json.Should().NotContain("\"nullable_field\"");
    }

    [Fact]
    public void DefaultJsonSettings_RoundTrip_PreservesValues()
    {
        var settings = SerializationHelpers.DefaultJsonSettings;
        var payload = new SamplePayload { MyProperty = "test", NestedValue = 99 };

        var json = JsonConvert.SerializeObject(payload, settings);
        var result = JsonConvert.DeserializeObject<SamplePayload>(json, settings);

        result.MyProperty.Should().Be("test");
        result.NestedValue.Should().Be(99);
    }

    // ===== Pattern: SerializationHelpers.WriteAsJson — JsonSerializer.Create(settings).Serialize(jsonWriter, payload) =====

    [Fact]
    public void WriteAsJson_Pattern_SerializesViaStreamingWriter()
    {
        // Exact pattern from SerializationHelpers.cs line 36-42:
        // using var streamWriter = new StreamWriter(...)
        // using var jsonWriter = new JsonTextWriter(streamWriter) { CloseOutput = false }
        // var serializer = JsonSerializer.Create(serializationSettings);
        // serializer.Serialize(jsonWriter, payload);
        var settings = SerializationHelpers.DefaultJsonSettings;
        var payload = new SamplePayload { MyProperty = "streamed", NestedValue = 7 };

        using var ms = new MemoryStream();
        using (var streamWriter = new StreamWriter(ms, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
        using (var jsonWriter = new JsonTextWriter(streamWriter) { CloseOutput = false })
        {
            var serializer = JsonSerializer.Create(settings);
            serializer.Serialize(jsonWriter, payload);
        }

        var json = Encoding.UTF8.GetString(ms.ToArray());
        // Remove BOM if present
        if (json.Length > 0 && json[0] == '\uFEFF')
        {
            json = json.Substring(1);
        }

        json.Should().Contain("\"my_property\":\"streamed\"");
        json.Should().Contain("\"nested_value\":7");
    }

    // ===== Pattern: SpanMessagePackFormatter.WriteJsonEvents — SerializeObject with custom SpanEventConverter =====

    [Fact]
    public void SpanEvents_SerializeWithSpanEventConverter_ProducesExpectedJson()
    {
        // Exact pattern from SpanMessagePackFormatter.cs line 462-463:
        // var settings = new JsonSerializerSettings { Converters = new List<JsonConverter> { new SpanEventConverter() }, Formatting = Formatting.None };
        // var eventsJson = JsonConvert.SerializeObject(spanModel.Span.SpanEvents, settings);
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Datadog.Trace.Util.SpanEventConverter() },
            Formatting = Formatting.None,
        };

        var events = new List<SpanEvent>
        {
            new("request.start", new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), new List<KeyValuePair<string, object>>
            {
                new("http.method", "GET"),
                new("http.status_code", 200),
                new("is_error", false),
                new("latency_ms", 42.5),
            }),
        };

        var json = JsonConvert.SerializeObject(events, settings);

        json.Should().Contain("\"name\":\"request.start\"");
        json.Should().Contain("\"time_unix_nano\":");
        json.Should().Contain("\"attributes\":");
        json.Should().Contain("\"http.method\":\"GET\"");
        json.Should().Contain("\"http.status_code\":200");
        json.Should().Contain("\"is_error\":false");
        json.Should().Contain("\"latency_ms\":42.5");
    }

    [Fact]
    public void SpanEvents_SerializeWithSpanEventConverter_NoAttributes()
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Datadog.Trace.Util.SpanEventConverter() },
            Formatting = Formatting.None,
        };

        var events = new List<SpanEvent>
        {
            new("simple.event", new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)),
        };

        var json = JsonConvert.SerializeObject(events, settings);

        json.Should().Contain("\"name\":\"simple.event\"");
        json.Should().NotContain("\"attributes\"");
    }

    [Fact]
    public void SpanEvents_SerializeWithSpanEventConverter_ArrayAttributes()
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Datadog.Trace.Util.SpanEventConverter() },
            Formatting = Formatting.None,
        };

        var events = new List<SpanEvent>
        {
            new("event.with.arrays", new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), new List<KeyValuePair<string, object>>
            {
                new("tags", new[] { "tag1", "tag2" }),
                new("counts", new[] { 1, 2, 3 }),
            }),
        };

        var json = JsonConvert.SerializeObject(events, settings);

        json.Should().Contain("\"tags\":[\"tag1\",\"tag2\"]");
        json.Should().Contain("\"counts\":[1,2,3]");
    }

    // ===== Pattern: ProcessHelpers.CommandOutput — [JsonProperty] + [JsonIgnore] =====

    [Fact]
    public void CommandOutput_RoundTrip_PreservesFields()
    {
        // Pattern from GitCommandHelper.cs: Serialize/Deserialize ProcessHelpers.CommandOutput
        // CommandOutput has [JsonProperty("output")], [JsonProperty("error")], [JsonProperty("exitCode")], [JsonProperty("timedOut")]
        // and [JsonIgnore] on Cached
        var output = new CommandOutputTestModel
        {
            Output = "commit abc123",
            Error = string.Empty,
            ExitCode = 0,
            TimedOut = false,
            Cached = true, // Should be ignored
        };

        var json = JsonConvert.SerializeObject(output);
        json.Should().Contain("\"output\":\"commit abc123\"");
        json.Should().Contain("\"exitCode\":0");
        json.Should().Contain("\"timedOut\":false");
        json.Should().NotContain("\"Cached\"").And.NotContain("\"cached\"");

        var result = JsonConvert.DeserializeObject<CommandOutputTestModel>(json);
        result.Output.Should().Be("commit abc123");
        result.Error.Should().BeEmpty();
        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    // ===== Test models mirroring internal types =====

    private struct ApiResponseTestModel
    {
        [JsonProperty("rate_by_service")]
        public Dictionary<string, float> RateByService { get; set; }
    }

    private class SamplePayload
    {
        public string MyProperty { get; set; }

        public int NestedValue { get; set; }

        public string NullableField { get; set; }
    }

    private class CommandOutputTestModel
    {
        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("exitCode")]
        public int ExitCode { get; set; }

        [JsonProperty("timedOut")]
        public bool TimedOut { get; set; }

        [JsonIgnore]
        public bool Cached { get; set; }
    }
}
