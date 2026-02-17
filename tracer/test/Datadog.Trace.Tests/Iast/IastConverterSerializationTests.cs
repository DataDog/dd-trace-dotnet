// <copyright file="IastConverterSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using Datadog.Trace.Iast;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Iast;

/// <summary>
/// Baseline serialization tests for IAST custom JSON converters.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class IastConverterSerializationTests
{
    // ===== SourceJsonConverter =====

    [Fact]
    public void SourceJsonConverter_ReadJson_ParsesSourceFromJson()
    {
        // SourceJsonConverter uses JObject.Load(reader) and bracket access
        var converter = new SourceJsonConverter(maxValueLength: 200);
        // language=json
        var json = """{"origin":"http.request.parameter","name":"user_input","value":"test-value"}""";

        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        jsonReader.Read(); // advance to start of object

        var result = converter.ReadJson(jsonReader, typeof(Source), null, false, JsonSerializer.CreateDefault());

        result.Should().NotBeNull();
        result.Origin.Should().Be(SourceType.RequestParameterValue);
        result.Name.Should().Be("user_input");
        result.Value.Should().Be("test-value");
    }

    [Fact]
    public void SourceJsonConverter_ReadJson_HandlesNullFields()
    {
        var converter = new SourceJsonConverter(maxValueLength: 200);
        // language=json
        var json = """{"origin":"http.request.body"}""";

        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        jsonReader.Read();

        var result = converter.ReadJson(jsonReader, typeof(Source), null, false, JsonSerializer.CreateDefault());

        result.Should().NotBeNull();
        result.Origin.Should().Be(SourceType.RequestBody);
        result.Name.Should().BeNull();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void SourceJsonConverter_WriteJson_WritesNonRedactedSource()
    {
        var converter = new SourceJsonConverter(maxValueLength: 200);
        var source = new Source(SourceType.RequestParameterValue, "param1", "value1");

        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        using var jsonWriter = new JsonTextWriter(stringWriter);

        converter.WriteJson(jsonWriter, source, JsonSerializer.CreateDefault());
        jsonWriter.Flush();

        var json = sb.ToString();
        json.Should().Contain("\"origin\":\"http.request.parameter\"");
        json.Should().Contain("\"name\":\"param1\"");
        json.Should().Contain("\"value\":\"value1\"");
    }

    [Fact]
    public void SourceJsonConverter_WriteJson_WritesRedactedSource()
    {
        var converter = new SourceJsonConverter(maxValueLength: 200);
        var source = new Source(SourceType.RequestHeaderValue, "auth", "secret-token");
        source.MarkAsRedacted();

        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        using var jsonWriter = new JsonTextWriter(stringWriter);

        converter.WriteJson(jsonWriter, source, JsonSerializer.CreateDefault());
        jsonWriter.Flush();

        var json = sb.ToString();
        json.Should().Contain("\"origin\":\"http.request.header\"");
        json.Should().Contain("\"name\":\"auth\"");
        json.Should().Contain("\"redacted\":true");
        json.Should().Contain("\"pattern\":");
        json.Should().NotContain("\"value\":\"secret-token\"");
    }

    // ===== EvidenceJsonConverter =====

    [Fact]
    public void EvidenceJsonConverter_WriteJson_NoRanges_WritesValueOnly()
    {
        var converter = new EvidenceJsonConverter(maxValueLength: 200, redactionEnabled: false);
        var evidence = new Evidence("SELECT * FROM Users");

        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        using var jsonWriter = new JsonTextWriter(stringWriter);

        converter.WriteJson(jsonWriter, evidence, JsonSerializer.CreateDefault());
        jsonWriter.Flush();

        var json = sb.ToString();
        json.Should().Contain("\"value\":\"SELECT * FROM Users\"");
        json.Should().NotContain("valueParts");
    }

    [Fact]
    public void EvidenceJsonConverter_WriteJson_NullEvidence_WritesNull()
    {
        var converter = new EvidenceJsonConverter(maxValueLength: 200, redactionEnabled: false);

        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        using var jsonWriter = new JsonTextWriter(stringWriter);

        converter.WriteJson(jsonWriter, null, JsonSerializer.CreateDefault());
        jsonWriter.Flush();

        sb.ToString().Should().Be("null");
    }

    [Fact]
    public void EvidenceJsonConverter_WithRanges_WritesValueParts()
    {
        var converter = new EvidenceJsonConverter(maxValueLength: 200, redactionEnabled: false);
        var source = new Source(SourceType.RequestParameterValue, "input", "tainted");
        source.SetInternalId(0);
        var ranges = new[] { new Range(26, 7, source) };
        var evidence = new Evidence("SELECT * FROM Users WHERE tainted", ranges);

        var sb = new StringBuilder();
        using var stringWriter = new StringWriter(sb);
        using var jsonWriter = new JsonTextWriter(stringWriter);

        converter.WriteJson(jsonWriter, evidence, JsonSerializer.CreateDefault());
        jsonWriter.Flush();

        var json = sb.ToString();
        json.Should().Contain("\"valueParts\"");
        json.Should().Contain("\"value\":\"SELECT * FROM Users WHERE \"");
        json.Should().Contain("\"source\":0");
        json.Should().Contain("\"value\":\"tainted\"");
    }
}
