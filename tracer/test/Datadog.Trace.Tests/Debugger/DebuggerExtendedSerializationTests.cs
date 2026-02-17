// <copyright file="DebuggerExtendedSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;
using FluentAssertions;
using Xunit;
using SymRoot = Datadog.Trace.Debugger.Symbols.Model.Root;
using SymScope = Datadog.Trace.Debugger.Symbols.Model.Scope;

namespace Datadog.Trace.Tests.Debugger;

/// <summary>
/// Baseline serialization tests for extended Debugger JSON patterns.
/// Covers: DiagnosticsUploader (SerializeObject for upload), SymbolsUploader
/// (SerializeObject with NullValueHandling.Ignore + JsonSerializer.Create for streaming),
/// SpanEventConverter (JObject construction + JToken.FromObject), ProbeExpressionParser
/// (JsonTextReader forward-only parsing).
/// </summary>
public class DebuggerExtendedSerializationTests
{
    // ===== Pattern: DiagnosticsUploader — diagnostics.Select(JsonConvert.SerializeObject) =====

    [Fact]
    public void DiagnosticsUploader_SerializeObject_DiagnosticsModels()
    {
        // Exact pattern from DiagnosticsUploader.cs line 38:
        // _diagnosticsBatchUploader.Upload(diagnostics.Select(JsonConvert.SerializeObject))
        // Each diagnostic is serialized individually using default settings
        var diagnostic = new DiagnosticTestModel
        {
            ProbeId = "probe-123",
            RuntimeId = "runtime-abc",
            Status = "INSTALLED",
            Message = "Probe installed successfully",
        };

        var json = JsonConvert.SerializeObject(diagnostic);
        json.Should().Contain("\"ProbeId\":\"probe-123\"");
        json.Should().Contain("\"RuntimeId\":\"runtime-abc\"");
        json.Should().Contain("\"Status\":\"INSTALLED\"");
        json.Should().Contain("\"Message\":\"Probe installed successfully\"");
    }

    // ===== Pattern: SymbolsUploader — JsonConvert.SerializeObject(root) then JsonSerializer.Create(settings) for streaming =====

    [Fact]
    public void SymbolsUploader_SerializeObject_Root_DefaultSettings()
    {
        // Exact pattern from SymbolsUploader.cs line 268:
        // var rootAsString = JsonConvert.SerializeObject(root);
        // This uses DEFAULT settings (no snake_case, no NullValueHandling) to produce the full root JSON
        // that is then split for chunked upload
        var root = new SymRoot
        {
            Service = "my-service",
            Env = "prod",
            Language = "dotnet",
            Version = "1.0.0",
            Scopes = [new SymScope { ScopeType = ScopeType.Assembly, Name = "MyAssembly", Scopes = [] }],
        };

        var json = JsonConvert.SerializeObject(root);

        // Default settings: PascalCase property names (from [JsonProperty] on the model)
        json.Should().Contain("\"service\":");  // Root has [JsonProperty("service")]
        json.Should().Contain("\"env\":");
        json.Should().Contain("\"language\":");
        json.Should().Contain("\"version\":");
        json.Should().Contain("\"scopes\":");
    }

    [Fact]
    public void SymbolsUploader_JsonSerializer_NullHandlingIgnore_Streaming()
    {
        // Exact pattern from SymbolsUploader.cs line 77:
        // _jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        // and line 280:
        // var serializer = JsonSerializer.Create(_jsonSerializerSettings);
        // Used for streaming class-level scopes into the chunked payload
        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        var serializer = JsonSerializer.Create(settings);

        var classScope = new SymScope
        {
            ScopeType = ScopeType.Class,
            Name = "MyNamespace.MyClass",
            SourceFile = "MyClass.cs",
            StartLine = 10,
            EndLine = 100,
            LanguageSpecifics = new LanguageSpecifics { AccessModifiers = ["public"] },
            Scopes = null, // should be omitted
        };

        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        using (var writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, classScope);
        }

        var json = sb.ToString();
        json.Should().Contain("\"name\":\"MyNamespace.MyClass\"");
        json.Should().Contain("\"source_file\":\"MyClass.cs\"");
        // NullValueHandling.Ignore — null Scopes should be omitted
        json.Should().NotContain("\"scopes\":null");
    }

    // ===== Pattern: ProbeExpressionParser — JsonTextReader forward-only parsing =====

    [Fact]
    public void ProbeExpressionParser_JsonTextReader_ForwardOnlyParsing()
    {
        // Exact pattern from ProbeExpressionParser.cs line 561-564:
        // var reader = new JsonTextReader(new StringReader(expressionJson));
        // SetReaderAtExpressionStart(reader);
        // var finalExpr = ParseRoot(reader, scopeMembers);
        // The parser uses reader.Read(), reader.TokenType, reader.Value to navigate probe expressions
        // language=json
        var expressionJson = """{"dsl":"ref","json":{"eq":[{"ref":"@return"},{"val":42}]}}""";

        using var sr = new StringReader(expressionJson);
        using var reader = new JsonTextReader(sr);

        // Simulate the forward-only parsing pattern used by ProbeExpressionParser
        var tokens = new List<(JsonToken Type, object Value)>();
        while (reader.Read())
        {
            tokens.Add((reader.TokenType, reader.Value));
        }

        // Verify we get the expected token sequence for parsing
        tokens.Should().Contain(t => t.Type == JsonToken.PropertyName && (string)t.Value == "dsl");
        tokens.Should().Contain(t => t.Type == JsonToken.String && (string)t.Value == "ref");
        tokens.Should().Contain(t => t.Type == JsonToken.PropertyName && (string)t.Value == "eq");
        tokens.Should().Contain(t => t.Type == JsonToken.Integer && (long)t.Value == 42L);
    }

    // ===== Pattern: SpanEventConverter — JObject + JToken.FromObject + writer.WriteToken =====

    [Fact]
    public void SpanEventConverter_JObject_JTokenFromObject_WriteToken()
    {
        // Exact pattern from SpanEventConverter.cs line 20-47:
        // var eventJObject = new JObject();
        // eventJObject.Add("name", value.Name);
        // eventJObject.Add("time_unix_nano", value.Timestamp.ToUnixTimeNanoseconds());
        // var jObject = new JObject(acceptedAttr.Select(kvp => new JProperty(kvp.Key, JToken.FromObject(kvp.Value))));
        // eventJObject.Add("attributes", jObject);
        // writer.WriteToken(eventJObject.CreateReader());
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Datadog.Trace.Util.SpanEventConverter() },
            Formatting = Formatting.None,
        };

        // Test with various attribute value types that SpanEventConverter.IsAllowedType accepts
        var events = new List<SpanEvent>
        {
            new("test.event", new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero), new List<KeyValuePair<string, object>>
            {
                new("string_attr", "hello"),
                new("int_attr", 42),
                new("long_attr", 123456789L),
                new("double_attr", 3.14),
                new("float_attr", 2.5f),
                new("bool_attr", true),
                new("byte_attr", (byte)255),
                new("short_attr", (short)32767),
                new("char_attr", 'A'),
            }),
        };

        var json = JsonConvert.SerializeObject(events, settings);

        json.Should().Contain("\"string_attr\":\"hello\"");
        json.Should().Contain("\"int_attr\":42");
        json.Should().Contain("\"long_attr\":123456789");
        json.Should().Contain("\"bool_attr\":true");
        json.Should().Contain("\"char_attr\":\"A\""); // char serializes as string
    }

    [Fact]
    public void SpanEventConverter_FiltersDisallowedTypes()
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Datadog.Trace.Util.SpanEventConverter() },
            Formatting = Formatting.None,
        };

        // SpanEventConverter.IsAllowedType filters: null values, object[] arrays, empty arrays, multidimensional arrays
        var events = new List<SpanEvent>
        {
            new("filtered.event", new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero), new List<KeyValuePair<string, object>>
            {
                new("allowed", "yes"),
                new("null_value", null),           // filtered out
                new("empty_key", "ok"),
            }),
        };

        var json = JsonConvert.SerializeObject(events, settings);

        json.Should().Contain("\"allowed\":\"yes\"");
        json.Should().Contain("\"empty_key\":\"ok\"");
        json.Should().NotContain("null_value");
    }

    // ===== Test models =====

    private sealed class DiagnosticTestModel
    {
        public string ProbeId { get; set; }

        public string RuntimeId { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }
    }
}
