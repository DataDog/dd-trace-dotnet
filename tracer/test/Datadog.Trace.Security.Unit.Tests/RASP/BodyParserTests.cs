// <copyright file="BodyParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class BodyParserTests
{
    [Fact]
    public void Parse_NullString_ReturnsNull()
    {
        var result = BodyParser.Parse(null);
        result.Should().BeNull();

        result = ParseBody(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var result = ParseBody(string.Empty);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidSimpleObject_ParsesCorrectly()
    {
        var json = @"{""name"":""test"",""age"":30}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, object>>();
        var dict = (Dictionary<string, object>)result!;
        dict.Should().ContainKey("name");
        dict["name"].Should().Be("test");
        dict.Should().ContainKey("age");
        dict["age"].Should().Be(30.0);
    }

    [Fact]
    public void Parse_ValidArray_ParsesCorrectly()
    {
        var json = @"[1,2,3,4,5]";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<List<object>>();
        var list = (List<object>)result!;
        list.Should().HaveCount(5);
        list[0].Should().Be(1.0);
        list[4].Should().Be(5.0);
    }

    [Fact]
    public void Parse_ValidNestedObject_ParsesCorrectly()
    {
        var json = @"{""user"":{""name"":""John"",""age"":30}}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict.Should().ContainKey("user");
        dict["user"].Should().BeOfType<Dictionary<string, object>>();
        var user = (Dictionary<string, object>)dict["user"]!;
        user["name"].Should().Be("John");
        user["age"].Should().Be(30.0);
    }

    [Fact]
    public void Parse_MixedTypes_ParsesCorrectly()
    {
        var json = JsonTestData.GenerateMixedStructure();
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict["name"].Should().Be("test");
        dict["age"].Should().Be(30.0);
        dict["active"].Should().Be(true);
        dict["balance"].Should().Be(100.50);
        dict["metadata"].Should().BeNull();
        dict["tags"].Should().BeOfType<List<object>>();
        dict["address"].Should().BeOfType<Dictionary<string, object>>();
    }

    [Fact]
    public void Parse_BooleanValues_ParsesCorrectly()
    {
        var json = @"{""isActive"":true,""isDeleted"":false}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict["isActive"].Should().Be(true);
        dict["isDeleted"].Should().Be(false);
    }

    [Fact]
    public void Parse_NullValue_ParsesCorrectly()
    {
        var json = @"{""value"":null}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict.Should().ContainKey("value");
        dict["value"].Should().BeNull();
    }

    [Fact]
    public void Parse_NumberTypes_ParsesAsDouble()
    {
        var json = @"{""int"":42,""float"":3.14,""negative"":-10}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict["int"].Should().Be(42.0);
        dict["float"].Should().Be(3.14);
        dict["negative"].Should().Be(-10.0);
    }

    [Fact]
    public void Parse_MaxDepth64_TruncatesDeepObjects()
    {
        var json = JsonTestData.GenerateNestedJson(65);
        var result = ParseBody(json);

        result.Should().NotBeNull();

        // Navigate to the deepest accessible level (should be 64 levels)
        var current = result;
        int depth = 0;
        while (current is Dictionary<string, object> dict && dict.Count > 0)
        {
            depth++;
            var key = dict.Keys.First();
            current = dict[key];
        }

        // Should stop at depth 64 (max depth)
        depth.Should().BeLessOrEqualTo(64);
    }

    [Fact]
    public void Parse_MaxElements1000_TruncatesLargeArrays()
    {
        var json = JsonTestData.GenerateArrayJson(1001);
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<List<object>>();
        var list = (List<object>)result!;

        // Should stop at 1000 elements
        list.Should().HaveCountLessOrEqualTo(1000);
    }

    [Fact]
    public void Parse_MaxElements1000_TruncatesObjectsWithManyProperties()
    {
        var json = JsonTestData.GenerateObjectWithManyProperties(1001);
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, object>>();
        var dict = (Dictionary<string, object>)result!;

        // Should stop at approximately 1000 properties
        dict.Count.Should().BeLessOrEqualTo(1000);
    }

    [Fact]
    public void Parse_MaxStringSize1024_TruncatesLongStrings()
    {
        var json = JsonTestData.GenerateLargeStringJson(2000);
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        var longString = dict["longString"] as string;

        longString.Should().NotBeNull();
        longString!.Length.Should().Be(1024);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var json = @"{invalid json}";
        var result = ParseBody(json);

        // Should handle gracefully (may throw or return null depending on implementation)
        // Adjust based on actual behavior
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(@"{""key"":""value"",}")]  // Trailing comma
    [InlineData(@"{key: ""value""}")]      // Unquoted key
    [InlineData(@"{""key"": undefined}")]  // Undefined value
    public void Parse_MalformedJson_HandlesGracefully(string json)
    {
        var result = ParseBody(json);

        // Should not throw, may return null or partial result
        // Test ensures no exceptions are thrown
    }

    [Fact]
    public void Parse_EmptyObject_ParsesCorrectly()
    {
        var json = @"{}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, object>>();
        var dict = (Dictionary<string, object>)result!;
        dict.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyArray_ParsesCorrectly()
    {
        var json = @"[]";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<List<object>>();
        var list = (List<object>)result!;
        list.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnicodeCharacters_ParsesCorrectly()
    {
        var json = @"{""emoji"":""🔥"",""chinese"":""你好"",""special"":""\u0041\u0042""}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict["emoji"].Should().Be("🔥");
        dict["chinese"].Should().Be("你好");
        dict["special"].Should().Be("AB");
    }

    [Fact]
    public void Parse_EscapedCharacters_ParsesCorrectly()
    {
        var json = @"{""quote"":""\""quoted\"""",""newline"":""line1\nline2"",""tab"":""col1\tcol2""}";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var dict = (Dictionary<string, object>)result!;
        dict["quote"].Should().Be("\"quoted\"");
        dict["newline"].Should().Be("line1\nline2");
        dict["tab"].Should().Be("col1\tcol2");
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNull()
    {
        var json = "   \t\n   ";
        var result = ParseBody(json);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ArrayOfObjects_ParsesCorrectly()
    {
        var json = @"[{""id"":1,""name"":""first""},{""id"":2,""name"":""second""}]";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var list = (List<object>)result!;
        list.Should().HaveCount(2);
        list[0].Should().BeOfType<Dictionary<string, object>>();
        var first = (Dictionary<string, object>)list[0];
        first["id"].Should().Be(1.0);
        first["name"].Should().Be("first");
    }

    [Fact]
    public void Parse_NestedArrays_ParsesCorrectly()
    {
        var json = @"[[1,2],[3,4],[5,6]]";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        var list = (List<object>)result!;
        list.Should().HaveCount(3);
        list[0].Should().BeOfType<List<object>>();
        var first = (List<object>)list[0];
        first[0].Should().Be(1.0);
        first[1].Should().Be(2.0);
    }

    [Fact]
    public void Parse_PrimitiveString_ParsesCorrectly()
    {
        var json = @"""simple string""";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().Be("simple string");
    }

    [Fact]
    public void Parse_PrimitiveNumber_ParsesCorrectly()
    {
        var json = @"42";
        var result = ParseBody(json);

        result.Should().NotBeNull();
        result.Should().Be(42.0);
    }

    [Fact]
    public void Parse_PrimitiveBoolean_ParsesCorrectly()
    {
        var jsonTrue = @"true";
        var jsonFalse = @"false";

        var resultTrue = ParseBody(jsonTrue);
        var resultFalse = ParseBody(jsonFalse);

        resultTrue.Should().Be(true);
        resultFalse.Should().Be(false);
    }

    [Fact]
    public void Parse_PrimitiveNull_ReturnsNull()
    {
        var json = @"null";
        var result = ParseBody(json);

        result.Should().BeNull();
    }

    private object ParseBody(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        byte[] byteArray = Encoding.UTF8.GetBytes(json);
        using MemoryStream stream = new MemoryStream(byteArray);
        return BodyParser.Parse(stream);
    }
}
