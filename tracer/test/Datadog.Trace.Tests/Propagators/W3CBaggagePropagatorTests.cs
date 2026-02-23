// <copyright file="W3CBaggagePropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
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

    public static TheoryData<string, SerializableList<BaggageEntry>> InjectBaggageData
        => new()
        {
            // expected header after encoding ⬅️ input key/value pairs
            { string.Empty, [] },
            { string.Empty, [new("key", null)] },
            { string.Empty, [new("key", string.Empty)] },
            { string.Empty, [new(null, "value")] },
            { string.Empty, [new(string.Empty, "value")] },
            { "key1=value1,key2=value2", [new("key1", "value1"), new("key2", "value2")] },
            { "key1=value1=valid", [new("key1", "value1=valid")] },
            { "key1=value1%2Cvalid", [new("key1", "value1,valid")] },
            { "%20key1%20=%20value1%20", [new(" key1 ", " value1 ")] },     // encode whitespace
            { "key=%20", [new("key", " ")] },                               // encode whitespace
            { "%20key1%20=%20value%091", [new(" key1 ", " value\t1")] },    // encode whitespace
            { "key%F0%9F%90%B6=value%E6%88%91", [new("key🐶", "value我")] }, // encode unicode
        };

    public static TheoryData<string, SerializableList<BaggageEntry>> ExtractBaggageData
        => new()
        {
            // input header ➡️ expected key/value pairs after decoding
            // empty
            { null, null },
            { string.Empty, null },
            { " ", null },
            // invalid
            { "invalid", null },
            { "invalid;", null },
            { "invalid=", null },
            { "invalid= ", null },
            { "invalid= ;", null },
            { " =invalid", null },
            { " ;=invalid", null },
            { "=", null },
            { " = ", null },
            { ";", null },
            // valid + invalid
            { "key1=value1,", null },
            { "key1=value1,key2", null },
            { "key1=value1,key2=", null },
            { "key1=value1,=value", null },
            { "key1=value1,=", null },
            { "key1=value1,key2;a=value2", null },
            // invalid + valid
            { ",key2=value2", null },
            { "key1,key2=value2", null },
            { "key1=,key2=value2", null },
            { "=value1,key2=value2", null },
            { "=,key2=value2", null },
            { "key1;a=value1,key2=value2", null },
            // valid
            { "valid=%20", [new("valid", " ")] },
            { "%20=valid", [new(" ", "valid")] },
            { "%20=%20", [new(" ", " ")] },
            { "key1=value1,key2=value2", [new("key1", "value1"), new("key2", "value2")] },
            { "key1=value1, key2 = value2;property1;property2, key3=value3; propertyKey=propertyValue", [new("key1", "value1"), new("key2", "value2"), new("key3", "value3")] }, // W3C metadata/property not currently supported so the values are discarded
            { "key1=value1%2Cvalid", [new("key1", "value1,valid")] },
            { "key1=value1=valid", [new("key1", "value1=valid")] },
            { "%20key1%20=%20value%091", [new(" key1 ", " value\t1")] },                          // encoded whitespace
            { "key1 = value1, key2 = value\t2 ", [new("key1", "value1"), new("key2", "value\t2")] }, // whitespace not encoded
            { "key%F0%9F%90%B6=value%E6%88%91", [new("key🐶", "value我")] },                       // encoded unicode
            { "key🐶=value我", [new("key🐶", "value我")] },                                         // unicode not encoded
        };

    [Theory]
    // keys
    [InlineData(null, true, "")]                                                                     // null
    [InlineData("", true, "")]                                                                       // empty string
    [InlineData("abcd", true, "abcd")]                                                               // no chars to encode
    [InlineData("José 🐶\t我", true, "Jos%C3%A9%20%F0%9F%90%B6%09%E6%88%91")]                         // always encode whitespace and Unicode chars
    [InlineData("\",;\\()/:<=>?@[]{}", true, "%22%2C%3B%5C%28%29%2F%3A%3C%3D%3E%3F%40%5B%5D%7B%7D")] // other chars (NOTE: different from value)
    // values
    [InlineData(null, false, "")]                                             // null
    [InlineData("", false, "")]                                               // empty string
    [InlineData("abcd", false, "abcd")]                                       // no chars to encode
    [InlineData("José 🐶\t我", false, "Jos%C3%A9%20%F0%9F%90%B6%09%E6%88%91")] // always encode whitespace and Unicode chars
    [InlineData("\",;\\()/:<=>?@[]{}", false, "%22%2C%3B%5C()/:<=>?@[]{}")]   // other chars (NOTE: different from key)
    public void EncodeStringAndAppend(string value, bool isKey, string expected)
    {
        var sb = new StringBuilder();
        W3CBaggagePropagator.EncodeStringAndAppend(sb, value, isKey);
        sb.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abcd", "abcd")]
    [InlineData("a%62c%64", "abcd")]
    [InlineData("Jos%C3%A9%20%F0%9F%90%B6%09%E6%88%91", "José 🐶\t我")]
    [InlineData("José 🐶\t我", "José 🐶\t我")]
    public void Decode(string value, string expected)
    {
        W3CBaggagePropagator.Decode(value).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(InjectBaggageData))]
    public void CreateHeader(string expectedHeader, SerializableList<BaggageEntry> inputPairs)
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
    [InlineData("José", 25, "name=Jos%C3%A9")]             // using non-ascii to ensure we're applying the limit after encoding
    [InlineData("José", 26, "name=Jos%C3%A9,key2=value2")] // using non-ascii to ensure we're applying the limit after encoding
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
    public void ParseHeader(string inputHeader, SerializableList<BaggageEntry> expectedPairs)
    {
        var baggage = W3CBaggagePropagator.ParseHeader(inputHeader)!;

        if (expectedPairs is null)
        {
            baggage.Should().BeNull();
            return;
        }

        baggage.Should().NotBeNull();
        baggage.Should().HaveCount(expectedPairs.Values.Count);

        foreach (var pair in expectedPairs)
        {
            baggage[pair.Key].Should().Be(pair.Value);
        }
    }

    [Fact]
    public void Inject_IHeadersCollection()
    {
        var headers = new Mock<IHeadersCollection>();
        var context = CreatePropagationContext(baggageCount: 2);

        BaggagePropagator.Inject(context, headers.Object);

        headers.Verify(h => h.Set("baggage", "key1=value1,key2=value2"), Times.Once());
        headers.VerifyNoOtherCalls();
    }

    [Fact]
    public void Inject_IHeadersCollection_Empty()
    {
        var headers = new Mock<IHeadersCollection>();
        var context = CreatePropagationContext(baggageCount: 0);

        BaggagePropagator.Inject(context, headers.Object);

        // "baggage" header should not be set
        headers.VerifyNoOtherCalls();
    }

    [Fact]
    public void Inject_CarrierAndDelegate()
    {
        // using IHeadersCollection for convenience, but carrier could be any type
        var headers = new Mock<IHeadersCollection>();
        var context = CreatePropagationContext(baggageCount: 2);

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
    // valid + invalid
    [InlineData("key1=value1,")]
    [InlineData("key1=value1,key2")]
    [InlineData("key1=value1,key2=")]
    [InlineData("key1=value1,=value2")]
    [InlineData("key1=value1,=")]
    [InlineData("key1=value1,key2;a=value2")]
    // invalid + valid
    [InlineData(",key2=value2")]
    [InlineData("key1,key2=value2")]
    [InlineData("key1=,key2=value2")]
    [InlineData("=value1,key2=value2")]
    [InlineData("=,key2=value2")]
    [InlineData("key1;a=value1,key2=value2")]
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

    [Fact]
    public void Extract_NoHeader()
    {
        var headers = new Mock<IHeadersCollection>(MockBehavior.Strict);

        headers.Setup(h => h.GetValues("baggage"))
               .Returns([]);

        var context = BaggagePropagator.Extract(headers.Object);

        headers.Verify(h => h.GetValues("baggage"), Times.Once());
        context.Baggage.Should().BeNull();
        context.SpanContext.Should().BeNull();
    }

    private static PropagationContext CreatePropagationContext(int baggageCount)
    {
        var spanContext = new SpanContext(
            new TraceId(0x0123456789abcdef, 0x1122334455667788),
            987654321,
            SamplingPriorityValues.UserKeep,
            serviceName: null,
            origin: null);

        var baggage = new Baggage();

        for (int i = 0; i < baggageCount; i++)
        {
            baggage[$"key{i + 1}"] = $"value{i + 1}";
        }

        return new PropagationContext(spanContext, baggage);
    }
}
