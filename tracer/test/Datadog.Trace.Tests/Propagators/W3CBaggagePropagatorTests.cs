// <copyright file="W3CBaggagePropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Propagators;

public class W3CBaggagePropagatorTests
{
    private static readonly SpanContextPropagator BaggagePropagator;

    static W3CBaggagePropagatorTests()
    {
        BaggagePropagator = SpanContextPropagatorFactory.GetSpanContextPropagator(
            [ContextPropagationHeaderStyle.W3CBaggage],
            [ContextPropagationHeaderStyle.W3CBaggage],
            propagationExtractFirst: false);
    }

    public static TheoryData<string, (string Key, string Value)[]> InjectBaggageData
        => new()
        {
            // expectedHeader, inputPairs
            { string.Empty, [] },
            { string.Empty, [("key1", null)] },
            { string.Empty, [("key1", string.Empty)] },
            { "key1=value1,key2=value2", [("key1", "value1"), ("key2", "value2")] },
            { "key1=value1=valid", [("key1", "value1=valid")] },
            { "key1=value1%2Cvalid", [("key1", "value1,valid")] },
            { "%20key1%20=%20value1%20", [(" key1 ", " value1 ")] },
            { "name=Jos%C3%A9", [("name", "José")] },
            { "key%F0%9F%90%B6=value%F0%9F%98%BA", [("key🐶", "value😺")] },
        };

    public static TheoryData<string, (string Key, string Value)[]> ExtractBaggageData
        => new()
        {
            // inputHeader, expectedPairs
            { null, null },
            { string.Empty, null },
            { " ", null },
            { "invalid", null },
            { "invalid=", null },
            { "=invalid", null },
            { "=", null },
            { "key1=value1,key2=value2", [("key1", "value1"), ("key2", "value2")] },
            { "key1=value1,invalid", [("key1", "value1")] },
            { "key1=value1%2Cvalid", [("key1", "value1,valid")] },
            { "key1=value1=valid", [("key1", "value1=valid")] },
            { "%20key1%20=%20value1%20", [(" key1 ", " value1 ")] },
            { "name=Jos%C3%A9", [("name", "José")] },
            { "key%F0%9F%90%B6=value%F0%9F%98%BA", [("key🐶", "value😺")] },
            { "key1 = value1, key2 = value2 ", [("key1", "value1"), ("key2", "value2")] },
        };

    [Theory]
    [InlineData("abcd", new char[0], "abcd")]
    [InlineData("abcd", new[] { 'x' }, "abcd")]
    [InlineData("abcd", new[] { 'b', 'd' }, "a%62c%64")]
    [InlineData("José", new char[0], "Jos%C3%A9")]
    [InlineData("🐶", new char[0], "%F0%9F%90%B6")]
    public void Encode(string value, char[] charsToEncode, string expected)
    {
        var result = W3CBaggagePropagator.Encode(value, [..charsToEncode]);
        result.Should().Be(expected);

        if (value == expected)
        {
            // ensure that the method is returning the same string instance when no encoding is needed
            ReferenceEquals(value, result).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("abcd", "abcd")]
    [InlineData("a%62c%64", "abcd")]
    [InlineData("Jos%C3%A9", "José")]
    [InlineData("%F0%9F%90%B6", "🐶")]
    public void Decode(string value, string expected)
    {
        W3CBaggagePropagator.Decode(value).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(InjectBaggageData))]
    public void CreateHeader(string expectedHeader, (string Key, string Value)[] inputPairs)
    {
        var baggage = new Baggage();

        foreach (var pair in inputPairs)
        {
            baggage[pair.Key] = pair.Value;
        }

        W3CBaggagePropagator.CreateHeader(baggage, W3CBaggagePropagator.DefaultMaximumBaggageItems, W3CBaggagePropagator.DefaultMaximumBaggageBytes)
                            .Should().Be(expectedHeader);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "key1=value1")]
    [InlineData(2, "key1=value1,key2=value2")]
    [InlineData(3, "key1=value1,key2=value2")]
    public void CreateHeader_MaxItems(int maxBaggageItems, string expected)
    {
        var baggage = new Baggage
        {
            { "key1", "value1" },
            { "key2", "value2" },
        };

        W3CBaggagePropagator.CreateHeader(baggage, maxBaggageItems, W3CBaggagePropagator.DefaultMaximumBaggageBytes)
                            .Should().Be(expected);
    }

    [Theory]
    [InlineData("Jose", 0, "")]
    [InlineData("Jose", 8, "")]
    [InlineData("Jose", 9, "name=Jose")]
    [InlineData("Jose", 20, "name=Jose")]
    [InlineData("Jose", 21, "name=Jose,key2=value2")]
    [InlineData("José", 25, "name=Jos%C3%A9")]
    [InlineData("José", 26, "name=Jos%C3%A9,key2=value2")]
    public void CreateHeader_MaxLength(string value, int maxBaggageLength, string expected)
    {
        var baggage = new Baggage
        {
            { "name", value },
            { "key2", "value2" },
        };

        W3CBaggagePropagator.CreateHeader(baggage, W3CBaggagePropagator.DefaultMaximumBaggageItems, maxBaggageLength)
                            .Should()
                            .Be(expected);
    }

    [Theory]
    [MemberData(nameof(ExtractBaggageData))]
    public void ExtractHeader(string inputHeader, (string Key, string Value)[] expectedPairs)
    {
        var baggage = W3CBaggagePropagator.ParseHeader(inputHeader);

        if (expectedPairs is null || expectedPairs.Length == 0)
        {
            baggage.Should().BeNull();
            return;
        }

        baggage.Should().NotBeNull();
        baggage!.Count.Should().Be(expectedPairs.Length);

        foreach (var pair in expectedPairs)
        {
            baggage[pair.Key].Should().Be(pair.Value);
        }
    }

    [Fact]
    public void Inject_IHeadersCollection()
    {
        var headers = new Mock<IHeadersCollection>();
        var context = CreatePropagationContext();

        BaggagePropagator.Inject(context, headers.Object);

        headers.Verify(h => h.Set("baggage", "key1=value1,key2=value2"), Times.Once());
        headers.VerifyNoOtherCalls();
    }

    [Fact]
    public void Inject_CarrierAndDelegate()
    {
        // using IHeadersCollection for convenience, but carrier could be any type
        var headers = new Mock<IHeadersCollection>();
        var context = CreatePropagationContext();

        BaggagePropagator.Inject(context, headers.Object, (carrier, name, value) => carrier.Set(name, value));

        headers.Verify(h => h.Set("baggage", "key1=value1,key2=value2"), Times.Once());
        headers.VerifyNoOtherCalls();
    }

    [Fact]
    public void Extract_IHeadersCollection()
    {
        var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

        headers.Setup(h => h.GetValues("baggage"))
               .Returns(["key1=value1,key2=value2"]);

        var result = BaggagePropagator.Extract(headers.Object);
        headers.Verify(h => h.GetValues("baggage"), Times.Once());

        var baggage = result.Baggage!;
        baggage.Should().NotBeNull();
        baggage["key1"].Should().Be("value1");
        baggage["key2"].Should().Be("value2");

        result.SpanContext.Should().BeNull();
    }

    [Fact]
    public void Extract_CarrierAndDelegate()
    {
        // using IHeadersCollection for convenience, but carrier could be any type
        var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

        headers.Setup(h => h.GetValues("baggage"))
               .Returns(["key1=value1,key2=value2"]);

        var result = BaggagePropagator.Extract(headers.Object, (carrier, name) => carrier.GetValues(name));
        headers.Verify(h => h.GetValues("baggage"), Times.Once());

        var baggage = result.Baggage!;
        baggage.Should().NotBeNull();
        baggage["key1"].Should().Be("value1");
        baggage["key2"].Should().Be("value2");

        result.SpanContext.Should().BeNull();
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("key=")]
    [InlineData("=value")]
    [InlineData("=")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Extract_InvalidFormat(string header)
    {
        var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

        headers.Setup(h => h.GetValues("baggage"))
               .Returns([header]);

        var context = BaggagePropagator.Extract(headers.Object);

        headers.Verify(h => h.GetValues("baggage"), Times.Once());
        context.Baggage.Should().BeNull();
        context.SpanContext.Should().BeNull();
    }

    private static PropagationContext CreatePropagationContext()
    {
        var spanContext = new SpanContext(
            new TraceId(0x0123456789abcdef, 0x1122334455667788),
            987654321,
            SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: null);

        var baggage = new Baggage
        {
            { "key1", "value1" },
            { "key2", "value2" },
        };

        return new PropagationContext(spanContext, baggage);
    }
}
