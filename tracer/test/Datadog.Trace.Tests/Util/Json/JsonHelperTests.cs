// <copyright file="JsonHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util.Json;

public class JsonHelperTests
{
    [Fact]
    public void SerializeObject_MatchesJsonConvert()
    {
        foreach (var value in GetDataToSerialize())
        {
            var expected = JsonConvert.SerializeObject(value);
            var actual = JsonHelper.SerializeObject(value);
            actual.Should().Be(expected);
        }
    }

    [Fact]
    public void SerializeObject_WithSettings_MatchesJsonConvert()
    {
        foreach (var value in GetDataToSerialize())
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

            var expected = JsonConvert.SerializeObject(value, settings);
            var actual = JsonHelper.SerializeObject(value, settings);
            actual.Should().Be(expected);
        }
    }

    [Fact]
    public void SerializeObject_WithFormatting_MatchesJsonConvert()
    {
        foreach (var value in GetDataToSerialize())
        {
            var settings = new JsonSerializerSettings();

            var expected = JsonConvert.SerializeObject(value, Formatting.Indented, settings);
            var actual = JsonHelper.SerializeObject(value, Formatting.Indented, settings);
            actual.Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("3.14")]
    [InlineData("true")]
    [InlineData("\"hello\"")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"key\":\"value\"}")]
    public void DeserializeObject_MatchesJsonConvert(string json)
    {
        var expected = JsonConvert.DeserializeObject(json);
        var actual = JsonHelper.DeserializeObject(json);

        if (expected is null)
        {
            actual.Should().BeNull();
        }
        else
        {
            actual.Should().BeEquivalentTo(expected);
        }
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("3.14", 3.14)]
    [InlineData("true", true)]
    [InlineData("\"hello\"", "hello")]
    public void DeserializeObject_Generic_MatchesJsonConvert(string json, object expected)
    {
        if (expected is int i)
        {
            JsonHelper.DeserializeObject<int>(json).Should().Be(i);
            JsonConvert.DeserializeObject<int>(json).Should().Be(i);
        }
        else if (expected is double d)
        {
            JsonHelper.DeserializeObject<double>(json).Should().Be(d);
            JsonConvert.DeserializeObject<double>(json).Should().Be(d);
        }
        else if (expected is bool b)
        {
            JsonHelper.DeserializeObject<bool>(json).Should().Be(b);
            JsonConvert.DeserializeObject<bool>(json).Should().Be(b);
        }
        else if (expected is string s)
        {
            JsonHelper.DeserializeObject<string>(json).Should().Be(s);
            JsonConvert.DeserializeObject<string>(json).Should().Be(s);
        }
    }

    [Fact]
    public void DeserializeObject_Generic_ComplexType()
    {
        var json = "{\"Name\":\"test\",\"Value\":123}";
        var expected = JsonConvert.DeserializeObject<TestDto>(json);
        var actual = JsonHelper.DeserializeObject<TestDto>(json);

        actual.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("{\"key\":\"value\"}")]
    [InlineData("{\"a\":1,\"b\":[1,2,3],\"c\":{\"nested\":true}}")]
    [InlineData("{}")]
    public void ParseJObject_String_MatchesJObjectParse(string json)
    {
        var expected = JObject.Parse(json);
        var actual = JsonHelper.ParseJObject(json);
        JToken.DeepEquals(actual, expected).Should().BeTrue();
    }

    [Theory]
    [InlineData("{\"key\":\"value\"}")]
    [InlineData("{\"a\":1,\"b\":[1,2,3],\"c\":{\"nested\":true}}")]
    [InlineData("{}")]
    public void ParseJObject_Bytes_MatchesJObjectParse(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var expected = JObject.Parse(json);
        var actual = JsonHelper.ParseJObject(bytes, Encoding.UTF8);
        JToken.DeepEquals(actual, expected).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)] // Formatting.None
    [InlineData(1)] // Formatting.Indented
    public void TokenToString_MatchesJTokenToString(int formattingInt)
    {
        var formatting = (Formatting)formattingInt;
        var token = JObject.Parse("{\"key\":\"value\",\"number\":42,\"nested\":{\"a\":true}}");
        var expected = token.ToString(formatting);
        var actual = JsonHelper.TokenToString(token, formatting);
        actual.Should().Be(expected);
    }

    [Fact]
    public void TokenToString_DefaultFormatting_IsIndented()
    {
        var token = JObject.Parse("{\"key\":\"value\"}");
        var expected = token.ToString(Formatting.Indented);
        var actual = JsonHelper.TokenToString(token);
        actual.Should().Be(expected);
    }

    [Fact]
    public void SerializeObject_RoundTrips_Correctly()
    {
        var original = new TestDto { Name = "roundtrip", Value = 999 };
        var json = JsonHelper.SerializeObject(original);
        var deserialized = JsonHelper.DeserializeObject<TestDto>(json);
        deserialized.Should().BeEquivalentTo(original);
    }

    private static object[] GetDataToSerialize() =>
    [
        null,
        42,
        3.14,
        true,
        "hello world",
        new[] { 1, 2, 3 },
        new { Name = "test", Value = 123 },
        new Dictionary<string, object> { ["key"] = "value", ["nested"] = new { A = 1 } },
    ];

    private class TestDto
    {
        public string Name { get; set; }

        public int Value { get; set; }
    }
}
