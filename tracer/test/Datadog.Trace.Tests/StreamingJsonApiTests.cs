// <copyright file="StreamingJsonApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

/// <summary>
/// Baseline tests for streaming JSON API usage patterns (JsonTextReader/JsonTextWriter).
/// These capture the exact streaming behavior before any JSON library migration.
/// Covers: JsonTokenizer (LinePosition-based value extraction), LogFormatter (escape:false,
/// WritePropertyName @-doubling, WriteValue type dispatch), SerializationHelpers
/// (JsonSerializer.Create streaming write with CloseOutput), MetricSeriesJsonConverter (nested arrays).
/// </summary>
public class StreamingJsonApiTests
{
    // ===== Pattern: JsonTokenizer — JsonTextReader with LinePosition-based value extraction =====

    [Fact]
    public void JsonTokenizer_LinePosition_CalculatesValueStart_StringToken()
    {
        // Exact pattern from JsonTokenizer.cs line 70-73:
        // var length = readerValue.Length;
        // var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0;
        // var start = reader.LinePosition - length - stringOffset;
        // ranges.Add(new Range(start, length));
        // language=json
        var json = """{"password":"secret123"}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "password"
        reader.Read(); // String "secret123"

        reader.TokenType.Should().Be(JsonToken.String);
        var readerValue = reader.Value.ToString();
        var length = readerValue.Length; // 9
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // 1 for strings
        var start = reader.LinePosition - length - stringOffset;

        // The value "secret123" starts at position 12 in the original string
        // (after {"password":")
        start.Should().Be(json.IndexOf("secret123", StringComparison.Ordinal));
        length.Should().Be(9);
    }

    [Fact]
    public void JsonTokenizer_LinePosition_CalculatesValueStart_IntegerToken()
    {
        // JsonTokenizer.cs: stringOffset = 0 for non-string tokens (Integer, Float, Boolean)
        // language=json
        var json = """{"count":42}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "count"
        reader.Read(); // Integer 42

        reader.TokenType.Should().Be(JsonToken.Integer);
        var readerValue = reader.Value.ToString();
        var length = readerValue.Length; // 2
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // 0 for integers
        var start = reader.LinePosition - length - stringOffset;

        start.Should().Be(json.IndexOf("42", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonTokenizer_LinePosition_CalculatesValueStart_FloatToken()
    {
        // language=json
        var json = """{"rate":3.14}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "rate"
        reader.Read(); // Float 3.14

        reader.TokenType.Should().Be(JsonToken.Float);
        var readerValue = reader.Value.ToString();
        var length = readerValue.Length;
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // 0 for floats
        var start = reader.LinePosition - length - stringOffset;

        start.Should().Be(json.IndexOf("3.14", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonTokenizer_LinePosition_CalculatesValueStart_BooleanToken()
    {
        // language=json
        var json = """{"active":true}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "active"
        reader.Read(); // Boolean true

        reader.TokenType.Should().Be(JsonToken.Boolean);
        var readerValue = reader.Value.ToString();
        var length = readerValue.Length;
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // 0 for booleans
        var start = reader.LinePosition - length - stringOffset;

        start.Should().Be(json.IndexOf("true", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonTokenizer_ForwardReadLoop_TokenTypeSwitch()
    {
        // Exact pattern from JsonTokenizer.cs line 42-57:
        // while (reader.Read()) { switch (reader.TokenType) { case String/Integer/Float/Boolean/PropertyName } }
        // language=json
        var json = """{"key":"val","num":99,"rate":1.5,"ok":false}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        var values = new List<(JsonToken Type, string Value, int Start)>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.Boolean:
                    var readerValue = reader.Value?.ToString();
                    if (readerValue is not null)
                    {
                        var length = readerValue.Length;
                        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0;
                        var start = reader.LinePosition - length - stringOffset;
                        values.Add((reader.TokenType, readerValue, start));
                    }

                    break;

                case JsonToken.PropertyName:
                    // JsonTokenizer also processes property names for sensitive key detection
                    values.Add((reader.TokenType, reader.Value?.ToString(), 0));
                    break;
            }
        }

        // Verify we captured all expected tokens
        values.Should().Contain(v => v.Type == JsonToken.PropertyName && v.Value == "key");
        values.Should().Contain(v => v.Type == JsonToken.String && v.Value == "val");
        values.Should().Contain(v => v.Type == JsonToken.PropertyName && v.Value == "num");
        values.Should().Contain(v => v.Type == JsonToken.Integer && v.Value == "99");
        values.Should().Contain(v => v.Type == JsonToken.PropertyName && v.Value == "rate");
        values.Should().Contain(v => v.Type == JsonToken.Float && v.Value == "1.5");
        values.Should().Contain(v => v.Type == JsonToken.PropertyName && v.Value == "ok");
        values.Should().Contain(v => v.Type == JsonToken.Boolean && v.Value == "False");
    }

    [Fact]
    public void JsonTokenizer_NewlineReplacedBySpace_ForSingleLinePosition()
    {
        // Exact pattern from JsonTokenizer.cs line 36:
        // value = value.Replace("\n", " ");
        // This ensures LinePosition works correctly (single line)
        var original = "{\"key\":\n\"value\"}";
        var normalized = original.Replace("\n", " ");

        using var sr = new StringReader(normalized);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Read(); // PropertyName "key"
        reader.Read(); // String "value"

        reader.TokenType.Should().Be(JsonToken.String);
        var readerValue = reader.Value.ToString();
        var length = readerValue.Length;
        var stringOffset = 1; // string token
        var start = reader.LinePosition - length - stringOffset;

        // After newline replacement, positions should be consistent on a single line
        start.Should().BeGreaterOrEqualTo(0);
        normalized.Substring(start, length).Should().Be("value");
    }

    // ===== Pattern: LogFormatter — JsonTextWriter with escape:false and WritePropertyName @-doubling =====

    [Fact]
    public void LogFormatter_WritePropertyName_EscapeFalse_ForKnownKeys()
    {
        // Exact pattern from LogFormatter.cs line 305:
        // writer.WritePropertyName("@t", escape: false);
        // Used for all known log property names: @t, @m, @i, @l, @x, ddsource, service, etc.
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartObject();

        // FormatLog pattern: all known keys use escape: false
        writer.WritePropertyName("@t", escape: false);
        writer.WriteValue("2024-01-15T00:00:00.0000000Z");

        writer.WritePropertyName("@m", escape: false);
        writer.WriteValue("Test message");

        writer.WritePropertyName("@i", escape: false);
        writer.WriteValue("a1b2c3d4");

        writer.WritePropertyName("@l", escape: false);
        writer.WriteValue("Warning");

        writer.WritePropertyName("@x", escape: false);
        writer.WriteValue("System.Exception: test");

        writer.WritePropertyName("ddsource", escape: false);
        writer.WriteValue("csharp");

        writer.WritePropertyName("service", escape: false);
        writer.WriteValue("my-service");

        writer.WritePropertyName("dd_env", escape: false);
        writer.WriteValue("prod");

        writer.WritePropertyName("dd_version", escape: false);
        writer.WriteValue("1.0.0");

        writer.WritePropertyName("host", escape: false);
        writer.WriteValue("myhost");

        writer.WritePropertyName("ddtags", escape: false);
        writer.WriteValue("env:prod,version:1.0.0");

        writer.WriteEndObject();
        writer.Flush();

        var json = sb.ToString();

        // All keys should appear in the JSON output
        json.Should().Contain("\"@t\":");
        json.Should().Contain("\"@m\":");
        json.Should().Contain("\"@i\":");
        json.Should().Contain("\"@l\":");
        json.Should().Contain("\"@x\":");
        json.Should().Contain("\"ddsource\":");
        json.Should().Contain("\"service\":");
        json.Should().Contain("\"dd_env\":");
        json.Should().Contain("\"dd_version\":");
        json.Should().Contain("\"host\":");
        json.Should().Contain("\"ddtags\":");

        // Verify it's valid JSON
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        parsed["@t"].Should().Be("2024-01-15T00:00:00.0000000Z");
        parsed["ddsource"].Should().Be("csharp");
    }

    [Fact]
    public void LogFormatter_WritePropertyName_DoublesLeadingAtSign()
    {
        // Exact pattern from LogFormatter.cs line 210-219:
        // if (name.Length > 0 && name[0] == '@') { name = '@' + name; }
        // writer.WritePropertyName(name);
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartObject();

        // Simulate the WritePropertyName helper for user-provided property names
        var name1 = "@timestamp";
        if (name1.Length > 0 && name1[0] == '@')
        {
            name1 = '@' + name1;
        }

        writer.WritePropertyName(name1);
        writer.WriteValue("2024-01-15");

        // Non-@ property should not be doubled
        var name2 = "normalKey";
        if (name2.Length > 0 && name2[0] == '@')
        {
            name2 = '@' + name2;
        }

        writer.WritePropertyName(name2);
        writer.WriteValue("value");

        writer.WriteEndObject();
        writer.Flush();

        var json = sb.ToString();
        json.Should().Contain("\"@@timestamp\":");
        json.Should().Contain("\"normalKey\":");
        json.Should().NotContain("\"@timestamp\":"); // Should be doubled
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hello world")]
    [InlineData("")]
    public void LogFormatter_WriteValue_String(string value)
    {
        // LogFormatter.cs line 232-236: if (value is string str) { writer.WriteValue(str); }
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartObject();
        writer.WritePropertyName("v");

        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value);
        }

        writer.WriteEndObject();
        writer.Flush();

        var json = sb.ToString();
        if (value is null)
        {
            json.Should().Be("""{"v":null}""");
        }
        else
        {
            json.Should().Contain($"\"{value}\"");
        }
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(-1, "-1")]
    [InlineData(0, "0")]
    public void LogFormatter_WriteValue_Int(int value, string expected)
    {
        // LogFormatter.cs line 242-244: case int → writer.WriteValue(Convert.ToInt64(value))
        var json = WriteValueToJson(value);
        json.Should().Be($"{{\"v\":{expected}}}");
    }

    [Fact]
    public void LogFormatter_WriteValue_AllIntegerTypes_ConvertToInt64()
    {
        // LogFormatter.cs line 242-244:
        // case int or uint or long or byte or sbyte or short or ushort:
        //     writer.WriteValue(Convert.ToInt64(value));
        WriteValueToJson((int)42).Should().Be("""{"v":42}""");
        WriteValueToJson((uint)42u).Should().Be("""{"v":42}""");
        WriteValueToJson((long)42L).Should().Be("""{"v":42}""");
        WriteValueToJson((byte)42).Should().Be("""{"v":42}""");
        WriteValueToJson((sbyte)42).Should().Be("""{"v":42}""");
        WriteValueToJson((short)42).Should().Be("""{"v":42}""");
        WriteValueToJson((ushort)42).Should().Be("""{"v":42}""");
    }

    [Fact]
    public void LogFormatter_WriteValue_Ulong()
    {
        // LogFormatter.cs line 245-247: case ulong → writer.WriteValue(ulongValue)
        // ulong can't safely be cast to long
        var json = WriteValueToJson(ulong.MaxValue);
        json.Should().Be($"{{\"v\":{ulong.MaxValue}}}");
    }

    [Fact]
    public void LogFormatter_WriteValue_Decimal()
    {
        // LogFormatter.cs line 248-250: case decimal → writer.WriteValue(decimalValue)
        var json = WriteValueToJson(123.456m);
        json.Should().Be("""{"v":123.456}""");
    }

    [Fact]
    public void LogFormatter_WriteValue_Double()
    {
        // LogFormatter.cs line 251-253: case double d → writer.WriteValue(d)
        var json = WriteValueToJson(3.14d);
        json.Should().Contain("3.14");
    }

    [Fact]
    public void LogFormatter_WriteValue_Float()
    {
        // LogFormatter.cs line 254-256: case float f → writer.WriteValue(f)
        var json = WriteValueToJson(2.5f);
        json.Should().Contain("2.5");
    }

    [Fact]
    public void LogFormatter_WriteValue_Bool()
    {
        // LogFormatter.cs line 257-259: case bool b → writer.WriteValue(b)
        WriteValueToJson(true).Should().Be("""{"v":true}""");
        WriteValueToJson(false).Should().Be("""{"v":false}""");
    }

    [Fact]
    public void LogFormatter_WriteValue_Char()
    {
        // LogFormatter.cs line 260-262: case char c → writer.WriteValue(c)
        var json = WriteValueToJson('A');
        json.Should().Be("""{"v":"A"}"""); // char serializes as a string
    }

    [Fact]
    public void LogFormatter_WriteValue_DateTime()
    {
        // LogFormatter.cs line 263-265: case DateTime dt → writer.WriteValue(dt)
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var json = WriteValueToJson(dt);
        json.Should().Contain("2024-06-15");
    }

    [Fact]
    public void LogFormatter_WriteValue_DateTimeOffset()
    {
        // LogFormatter.cs line 266-268: case DateTimeOffset dto → writer.WriteValue(dto)
        var dto = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var json = WriteValueToJson(dto);
        json.Should().Contain("2024-06-15");
    }

    [Fact]
    public void LogFormatter_WriteValue_TimeSpan()
    {
        // LogFormatter.cs line 269-271: case TimeSpan timeSpan → writer.WriteValue(timeSpan)
        var ts = TimeSpan.FromHours(2.5);
        var json = WriteValueToJson(ts);
        json.Should().Contain("02:30:00");
    }

    [Fact]
    public void LogFormatter_WriteValue_UnknownType_FallbackToConvertToString()
    {
        // LogFormatter.cs line 275: writer.WriteValue(Convert.ToString(value, CultureInfo.InvariantCulture))
        // For unrecognized types, falls back to Convert.ToString
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var json = WriteValueToJson(guid);
        json.Should().Contain("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void LogFormatter_GetJsonWriter_FormattingNone()
    {
        // Exact pattern from LogFormatter.cs line 278-283:
        // var writer = new JsonTextWriter(new StringWriter(builder));
        // writer.Formatting = Formatting.None;
        var sb = new StringBuilder();
        var writer = new JsonTextWriter(new StringWriter(sb));
        writer.Formatting = Formatting.None;

        writer.WriteStartObject();
        writer.WritePropertyName("key");
        writer.WriteValue("value");
        writer.WriteEndObject();
        writer.Flush();

        // Formatting.None means no whitespace/indentation
        sb.ToString().Should().Be("""{"key":"value"}""");
        writer.Close();
    }

    // ===== Pattern: LogFormatter.FormatCIVisibilityLog — ddsource, hostname, timestamp, status, message fields =====

    [Fact]
    public void LogFormatter_FormatCIVisibilityLog_FieldSequence()
    {
        // Exact pattern from LogFormatter.cs line 382-468:
        // FormatCIVisibilityLog writes: ddsource, hostname, timestamp, status, message,
        // dd.trace_id, dd.span_id, test.suite, test.name, test.bundle, service, ddtags
        var sb = new StringBuilder();
        using var writer = new JsonTextWriter(new StringWriter(sb)) { Formatting = Formatting.None };

        writer.WriteStartObject();

        writer.WritePropertyName("ddsource", escape: false);
        writer.WriteValue("dotnet");

        writer.WritePropertyName("hostname", escape: false);
        writer.WriteValue("myhost");

        writer.WritePropertyName("timestamp", escape: false);
        writer.WriteValue(1700000000000L); // Unix milliseconds

        writer.WritePropertyName("status", escape: false);
        writer.WriteValue("error");

        writer.WritePropertyName("message", escape: false);
        writer.WriteValue("Test failed: assertion error");

        writer.WritePropertyName("dd.trace_id", escape: false);
        writer.WriteValue("12345678901234567890");

        writer.WritePropertyName("dd.span_id", escape: false);
        writer.WriteValue("9876543210");

        writer.WritePropertyName("service", escape: false);
        writer.WriteValue("my-test-service");

        writer.WritePropertyName("ddtags", escape: false);
        writer.WriteValue("env:ci,datadog.product:citest");

        writer.WriteEndObject();
        writer.Flush();

        var json = sb.ToString();

        // Verify all CI visibility fields are present
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        parsed["ddsource"].ToString().Should().Be("dotnet");
        parsed["hostname"].ToString().Should().Be("myhost");
        parsed["timestamp"].Should().BeOfType<long>().Which.Should().Be(1700000000000L);
        parsed["status"].ToString().Should().Be("error");
        parsed["message"].ToString().Should().Be("Test failed: assertion error");
        parsed["dd.trace_id"].ToString().Should().Be("12345678901234567890");
        parsed["dd.span_id"].ToString().Should().Be("9876543210");
        parsed["service"].ToString().Should().Be("my-test-service");
        parsed["ddtags"].ToString().Should().Be("env:ci,datadog.product:citest");
    }

    // ===== Pattern: SerializationHelpers.WriteAsJson — JsonSerializer.Create + JsonTextWriter with CloseOutput =====

    [Fact]
    public void SerializationHelpers_WriteAsJson_StreamingSerialize_CloseOutputFalse()
    {
        // Exact pattern from SerializationHelpers.cs line 28-46:
        // using var streamWriter = new StreamWriter(streamToWriteTo, EncodingHelpers.Utf8NoBom, bufferSize: 1024, leaveOpen: true);
        // using var jsonWriter = new JsonTextWriter(streamWriter) { CloseOutput = false };
        // var serializer = JsonSerializer.Create(serializationSettings);
        // serializer.Serialize(jsonWriter, payload);
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            }
        };

        var payload = new WriteAsJsonTestPayload
        {
            ServiceName = "my-service",
            EnvironmentName = "production",
            NullableField = null,
            ItemCount = 42,
        };

        // Simulate the exact streaming pattern with CloseOutput = false
        using var ms = new MemoryStream();
        using (var streamWriter = new StreamWriter(ms, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
        using (var jsonWriter = new JsonTextWriter(streamWriter) { CloseOutput = false })
        {
            var serializer = JsonSerializer.Create(settings);
            serializer.Serialize(jsonWriter, payload);
        }

        // Stream should still be accessible (leaveOpen: true + CloseOutput: false)
        ms.Position = 0;
        var json = new StreamReader(ms).ReadToEnd();

        // Properties should be snake_case
        json.Should().Contain("\"service_name\":\"my-service\"");
        json.Should().Contain("\"environment_name\":\"production\"");
        json.Should().Contain("\"item_count\":42");
        // Null properties should be omitted
        json.Should().NotContain("\"nullable_field\"");
    }

    [Fact]
    public void SerializationHelpers_DefaultJsonSettings_SnakeCaseAndNullIgnore()
    {
        // Exact settings from SerializationHelpers.cs line 19-26:
        // public static readonly JsonSerializerSettings DefaultJsonSettings = new()
        // {
        //     NullValueHandling = NullValueHandling.Ignore,
        //     ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }
        // };
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            }
        };

        var payload = new WriteAsJsonTestPayload
        {
            ServiceName = "svc",
            EnvironmentName = null,
            NullableField = null,
            ItemCount = 0,
        };

        var json = JsonConvert.SerializeObject(payload, settings);

        // snake_case naming
        json.Should().Contain("\"service_name\":");
        json.Should().Contain("\"item_count\":");
        // Null fields omitted
        json.Should().NotContain("\"environment_name\"");
        json.Should().NotContain("\"nullable_field\"");
        // Zero is NOT omitted (NullValueHandling, not DefaultValueHandling)
        json.Should().Contain("\"item_count\":0");
    }

    // ===== Pattern: MetricSeriesJsonConverter — nested arrays via JsonTextWriter =====

    [Fact]
    public void MetricSeriesJsonConverter_NestedArrays_TimestampValuePairs()
    {
        // Pattern from MetricSeriesJsonConverter: writes [[timestamp, value], ...]
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartArray();

        writer.WriteStartArray();
        writer.WriteValue(1700000000L);
        writer.WriteValue(42);
        writer.WriteEndArray();

        writer.WriteStartArray();
        writer.WriteValue(1700000060L);
        writer.WriteValue(100);
        writer.WriteEndArray();

        writer.WriteEndArray();
        writer.Flush();

        sb.ToString().Should().Be("[[1700000000,42],[1700000060,100]]");
    }

    // ===== Pattern: JsonTextReader depth tracking (NamedRawFile generic deserialization) =====

    [Fact]
    public void JsonTextReader_NestedObjects_TracksDepth()
    {
        // Pattern from NamedRawFile: JsonTextReader → JsonSerializer.Deserialize<T>(reader)
        // Tests reader depth tracking for nested structures
        // language=json
        var json = """{"outer":{"inner":[1,2]}}""";

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);

        reader.Read(); // StartObject
        reader.Depth.Should().Be(0);

        reader.Read(); // PropertyName "outer"
        reader.Read(); // StartObject (inner)
        reader.Depth.Should().Be(1);

        reader.Read(); // PropertyName "inner"
        reader.Read(); // StartArray
        reader.Depth.Should().Be(2);

        reader.Read(); // 1
        reader.TokenType.Should().Be(JsonToken.Integer);
        reader.Depth.Should().Be(3);
    }

    [Fact]
    public void JsonTextWriter_WriteNull_ProducesNullLiteral()
    {
        // LogFormatter.cs line 228: writer.WriteNull();
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartObject();
        writer.WritePropertyName("nullField");
        writer.WriteNull();
        writer.WriteEndObject();
        writer.Flush();

        sb.ToString().Should().Be("""{"nullField":null}""");
    }

    // ===== Helper to invoke the LogFormatter.WriteValue type dispatch pattern =====

    private static string WriteValueToJson(object value)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        using var writer = new JsonTextWriter(sw) { Formatting = Formatting.None };

        writer.WriteStartObject();
        writer.WritePropertyName("v");

        // Reproduce the exact LogFormatter.WriteValue dispatch from line 224-276
        if (value is null)
        {
            writer.WriteNull();
        }
        else if (value is string str)
        {
            writer.WriteValue(str);
        }
        else if (value is ValueType)
        {
            switch (value)
            {
                case int or uint or long or byte or sbyte or short or ushort:
                    writer.WriteValue(Convert.ToInt64(value));
                    break;
                case ulong ulongValue:
                    writer.WriteValue(ulongValue);
                    break;
                case decimal decimalValue:
                    writer.WriteValue(decimalValue);
                    break;
                case double d:
                    writer.WriteValue(d);
                    break;
                case float f:
                    writer.WriteValue(f);
                    break;
                case bool b:
                    writer.WriteValue(b);
                    break;
                case char c:
                    writer.WriteValue(c);
                    break;
                case DateTime dt:
                    writer.WriteValue(dt);
                    break;
                case DateTimeOffset dto:
                    writer.WriteValue(dto);
                    break;
                case TimeSpan timeSpan:
                    writer.WriteValue(timeSpan);
                    break;
                default:
                    writer.WriteValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }
        else
        {
            writer.WriteValue(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        writer.WriteEndObject();
        writer.Flush();

        return sb.ToString();
    }

    // ===== Test models =====

    private sealed class WriteAsJsonTestPayload
    {
        public string ServiceName { get; set; }

        public string EnvironmentName { get; set; }

        public string NullableField { get; set; }

        public int ItemCount { get; set; }
    }
}
