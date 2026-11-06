// <copyright file="InjectedContextHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System;
using System.Text;
using System.Text.Json;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Serverless.AWS;

/// <summary>
/// Unit tests for InjectedContextHelper.
/// Fuzz section uses fixed seeds so CI is deterministic and results are reproducible.
/// To reproduce a failure, add the seed printed in the failure message as an InlineData.
/// </summary>
public class InjectedContextHelperTests
{
    private const string Carrier = "{\"x-datadog-trace-id\":\"123\",\"x-datadog-parent-id\":\"456\"}";

    private static readonly string[] StringPieces =
    {
        "a", "Z", "0", " ", "_", "-", ":", "{", "}", ",",
        "\\\"", "\\\\", "\\n", "\\u00e9", "\u00e9", "_datadog",
    };

    // -----------------------------------------------------------------------
    // EventBridge (_datadog in detail) and SQS (_datadog at the top level)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData($"{{\"detail-type\":\"order.created\",\"detail\":{{\"orderId\":42,\"_datadog\":{Carrier}}}}}", "{\"detail-type\":\"order.created\",\"detail\":{\"orderId\":42}}")]
    [InlineData($"{{\"detail-type\":\"e\",\"detail\":{{\"_datadog\":{Carrier}}}}}", "{\"detail-type\":\"e\",\"detail\":{}}")]
    [InlineData($"{{\"detail\":{{\"_datadog\":{Carrier},\"orderId\":42}}}}", "{\"detail\":{\"orderId\":42}}")]
    [InlineData($"{{\"orderId\":42,\"customer\":\"acme\",\"_datadog\":{Carrier}}}", "{\"orderId\":42,\"customer\":\"acme\"}")]
    public void EventBridgeAndSqs_WithCarrier_StripsCarrier(string input, string expected)
    {
        InjectedContextHelper.StripContext(input).Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // Nothing to strip: the same instance is returned, so the caller keeps the original stream
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("{\"detail-type\":\"order.created\",\"detail\":{\"orderId\":42}}")]
    [InlineData("{\"orderId\":42,\"customer\":\"acme\"}")]
    [InlineData("{\"note\":\"contains _datadog text\",\"count\":1}")]
    [InlineData("{\"{\\\"_datadog\\\":{}}\":1}")]
    [InlineData("{\"m\":\"{\\\"_datadog\\\":{\\\"a\\\":1}} trailing text\"}")]
    [InlineData("{\"note\":\"{\\\"_datadog\\\":{}} is an example {}\"}")]
    [InlineData("{\"note\":\"{see \\\"_datadog\\\":{\\\"x\\\":1} for details}\"}")]
    [InlineData("{\"note\":\"{\\\"_datadog\\\":{\\\"a\\\":1} \\\"b\\\"}\"}")]
    [InlineData("{\"m\":\"{\\\"_datadog\\\":{}}\\x\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{}\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{}} /* example */\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{},/* example */\\\"x\\\":1}\"}")]
    [InlineData("{\"body\":\"{'note':'see \\\"_datadog\\\":{} here'}\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{}},\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{},\\\"a\\\":1,}\"}")]
    [InlineData("{\"body\":\"{a:1,\\\"_datadog\\\":{}}\"}")]
    [InlineData("{\"body\":\"{\\\"_datadog\\\":{},\\\"n\\\":NaN}\"}")]
    [InlineData($"{{\"name\":\"\uFFFD\",\"_datadog\":{Carrier}}}")]
    [InlineData("{\"Message\":\"{\\\"_datadog\\\":{},\\\"s\\\":\\\"\\ud800\\\"}\"}")]
    public void NothingToStrip_ReturnsSameInstance(string input)
    {
        InjectedContextHelper.StripContext(input).Should().BeSameAs(input);
    }

    [Theory]
    [InlineData("{\"_datadog\":{\"x-datadog-trace-id\":\"1\"")]
    [InlineData("{\"_datadog\":{\"a\":1},\"x\":{\"_datadog\":{\"b\":2")]
    [InlineData("{\"_datadog\":{},\"x\":\"_datadog")]
    [InlineData("{\"_datadog\":{\"a\":1},\"x\":\"abc _datadog")]
    [InlineData("{\"_datadog\":{}")]
    [InlineData("{\"a\":1,\"_datadog\":{}},")]
    [InlineData("{'note':'see \"_datadog\":{} here'}")]
    public void MalformedJson_ReturnsSameInstance(string input)
    {
        InjectedContextHelper.StripContext(input).Should().BeSameAs(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_ReturnsInput(string input)
    {
        InjectedContextHelper.StripContext(input).Should().BeSameAs(input);
    }

    // -----------------------------------------------------------------------
    // SNS: _datadog is in a string-encoded "Message" field
    // -----------------------------------------------------------------------

    [Fact]
    public void Sns_WithCarrier_StripsFromEncodedMessage()
    {
        var innerJson = $"{{\"orderId\":42,\"_datadog\":{Carrier}}}";
        var encodedInner = JsonSerializer.Serialize(innerJson);
        var input = $"{{\"Type\":\"Notification\",\"Message\":{encodedInner}}}";

        var result = InjectedContextHelper.StripContext(input);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("Type").GetString().Should().Be("Notification");
        doc.RootElement.GetProperty("Message").GetString().Should().Be("{\"orderId\":42}");
    }

    // -----------------------------------------------------------------------
    // Customer data: _datadog key with non-object value must be kept
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("\"_datadog\":\"string-value\"")]
    [InlineData("\"_datadog\":42")]
    [InlineData("\"_datadog\":true")]
    [InlineData("\"_datadog\":null")]
    [InlineData("\"_datadog\":[1,2,3]")]
    public void NonObjectCarrier_IsKept(string kvPair)
    {
        var input = $"{{{kvPair},\"other\":1}}";
        InjectedContextHelper.StripContext(input).Should().BeSameAs(input);
    }

    // -----------------------------------------------------------------------
    // Edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedSnsInSqs_StripsAtAllLevels()
    {
        // SQS body contains SNS-style Message which itself contains _datadog
        var innerInner = $"{{\"data\":1,\"_datadog\":{Carrier}}}";
        var innerMessage = JsonSerializer.Serialize(innerInner);
        var sqsBody = $"{{\"Type\":\"Notification\",\"Message\":{innerMessage}}}";
        var encodedSqsBody = JsonSerializer.Serialize(sqsBody);
        var input = $"{{\"body\":{encodedSqsBody}}}";

        var result = InjectedContextHelper.StripContext(input);

        using var outer = JsonDocument.Parse(result);
        var body = outer.RootElement.GetProperty("body").GetString();
        body.Should().NotBeNull();
        using var sns = JsonDocument.Parse(body);
        sns.RootElement.GetProperty("Message").GetString().Should().Be("{\"data\":1}");
    }

    [Fact]
    public void MaxDepth_StripsWithinLimit_KeepsDeeperCarrier()
    {
        static string Wrap(string inner, int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                inner = $"{{\"m\":{JsonSerializer.Serialize(inner)}}}";
            }

            return inner;
        }

        var carrierJson = $"{{\"_datadog\":{Carrier}}}";

        // MaxDepth levels of string-encoded JSON: carrier is stripped at the innermost level
        var current = InjectedContextHelper.StripContext(Wrap(carrierJson, InjectedContextHelper.MaxDepth));
        for (int i = 0; i < InjectedContextHelper.MaxDepth; i++)
        {
            using var doc = JsonDocument.Parse(current);
            current = doc.RootElement.GetProperty("m").GetString();
            current.Should().NotBeNull();
        }

        current.Should().Be("{}");

        // One level deeper than MaxDepth: left untouched
        var beyondLimit = Wrap(carrierJson, InjectedContextHelper.MaxDepth + 1);
        InjectedContextHelper.StripContext(beyondLimit).Should().BeSameAs(beyondLimit);
    }

    [Theory]
    [InlineData("{\"a\":1,\"_datadog\":{},\"_datadog\":{}}", "{\"a\":1}")]
    [InlineData("{\"_datadog\":{},\"_datadog\":{}}", "{}")]
    [InlineData("{\"_datadog\":{},\"a\":1,\"_datadog\":{}}", "{\"a\":1}")]
    [InlineData("{\"a\":{\"_datadog\":{\"x\":\"}\"}},\"b\":[{\"_datadog\":{}}]}", "{\"a\":{},\"b\":[{}]}")]
    [InlineData("{ \"_datadog\" : { \"a\" : \"b\" } , \"z\" : 1 }", "{  \"z\" : 1 }")]
    [InlineData("{\"_datadog\":{\"a\":1},\"_datadog\":\"keep\"}", "{\"_datadog\":\"keep\"}")]
    public void EdgeCases_StripCorrectly(string input, string expected)
    {
        InjectedContextHelper.StripContext(input).Should().Be(expected);
    }

    [Fact]
    public void EncodedJson_WithEscapes_StripsCarrierAndPreservesValues()
    {
        var inner = "{\"_datadog\":{\"a\":1},\"s\":\"tab\\tquote\\\"slash\\/e\\u00e9\"}";
        var input = "{\"Message\":" + JsonSerializer.Serialize(inner) + "}";

        var result = InjectedContextHelper.StripContext(input);

        using var outer = JsonDocument.Parse(result);
        var message = outer.RootElement.GetProperty("Message").GetString();
        message.Should().NotBeNull();
        using var decoded = JsonDocument.Parse(message);
        decoded.RootElement.TryGetProperty("_datadog", out _).Should().BeFalse();
        decoded.RootElement.GetProperty("s").GetString().Should().Be("tab\tquote\"slash/e\u00e9");
    }

    [Fact]
    public void EncodedJson_DecodesEscapeSequencesInOuterLiteral()
    {
        var input = "{\"Message\":\"{\\n\\t\\\"_datadog\\\":{},\\\"k\\\":\\\"a\\/b\\\",\\\"\\u0041\\\":1}\"}";

        var result = InjectedContextHelper.StripContext(input);

        using var outer = JsonDocument.Parse(result);
        var message = outer.RootElement.GetProperty("Message").GetString();
        message.Should().NotBeNull();
        using var decoded = JsonDocument.Parse(message);
        decoded.RootElement.TryGetProperty("_datadog", out _).Should().BeFalse();
        decoded.RootElement.GetProperty("k").GetString().Should().Be("a/b");
        decoded.RootElement.GetProperty("A").GetInt32().Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // Fuzz / randomized section (seeded, deterministic, no new dependencies)
    //
    // Each test generates random JSON-like strings and asserts:
    //   1. StripContext never throws.
    //   2. If input contains no "_datadog" key, output equals input.
    //   3. Output never contains a "_datadog":{...} carrier (object value).
    // Preservation of non-object _datadog values is covered by NonObjectCarrier_IsKept.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(12345L)]
    [InlineData(99999L)]
    [InlineData(7777777L)]
    public void Fuzz_RandomPayloads_NeverThrowsAndPreservesInvariant(long seed)
    {
        var rng = new Random((int)(seed ^ (seed >> 32)));
        for (int i = 0; i < 500; i++)
        {
            var payload = GenerateRandomPayload(rng);
            string result = null;
            var act = () => result = InjectedContextHelper.StripContext(payload);
            act.Should().NotThrow($"seed={seed}, iteration={i}, input={payload}");

            result.Should().NotBeNull();
            var parse = () => JsonDocument.Parse(result).Dispose();
            parse.Should().NotThrow($"output must be valid JSON; seed={seed}, iteration={i}, input={payload}, result={result}");

            // If payload had no carrier key at all, result must equal input
            if (!payload.Contains("_datadog"))
            {
                result.Should().Be(payload, $"no carrier key in payload; seed={seed}, iteration={i}");
            }
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(12345L)]
    public void Fuzz_PayloadsWithCarrier_CarrierIsAbsentOrNonObjectInOutput(long seed)
    {
        var rng = new Random((int)(seed ^ (seed >> 32)));
        for (int i = 0; i < 300; i++)
        {
            var payload = GeneratePayloadWithCarrier(rng);
            var result = InjectedContextHelper.StripContext(payload);
            var parse = () => JsonDocument.Parse(result).Dispose();
            parse.Should().NotThrow($"output must be valid JSON; seed={seed}, iteration={i}, input={payload}, result={result}");

            // The result must not contain a "_datadog":{...} object value
            AssertNoCarrierObject(result, seed, i, payload);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string GenerateRandomPayload(Random rng)
    {
        var sb = new StringBuilder("{");
        int fields = rng.Next(0, 6);
        for (int i = 0; i < fields; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append('"').Append(RandomKey(rng)).Append("\":");
            AppendRandomValue(rng, sb, depth: 0);
        }

        return sb.Append('}').ToString();
    }

    private static string GeneratePayloadWithCarrier(Random rng)
    {
        var sb = new StringBuilder("{");
        int prefixFields = rng.Next(0, 4);
        for (int i = 0; i < prefixFields; i++)
        {
            sb.Append('"').Append(RandomKey(rng)).Append("\":");
            AppendRandomValue(rng, sb, depth: 0);
            sb.Append(',');
        }

        sb.Append($"\"_datadog\":{Carrier}");
        int suffixFields = rng.Next(0, 4);
        for (int i = 0; i < suffixFields; i++)
        {
            sb.Append(',').Append('"').Append(RandomKey(rng)).Append("\":");
            AppendRandomValue(rng, sb, depth: 0);
        }

        return sb.Append('}').ToString();
    }

    private static void AppendRandomValue(Random rng, StringBuilder sb, int depth)
    {
        var kind = rng.Next(0, depth < 2 ? 5 : 3);
        switch (kind)
        {
            case 0: sb.Append(rng.Next(-100, 1000)); break;
            case 1: sb.Append(rng.Next(0, 2) == 0 ? "true" : "false"); break;
            case 2: sb.Append('"').Append(RandomString(rng)).Append('"'); break;
            case 3:
                sb.Append('{');
                int n = rng.Next(0, 3);
                for (int i = 0; i < n; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('"').Append(RandomKey(rng)).Append("\":");
                    AppendRandomValue(rng, sb, depth + 1);
                }

                sb.Append('}');
                break;
            default: sb.Append("null"); break;
        }
    }

    private static string RandomKey(Random rng)
    {
        // Occasionally inject _datadog as a key name to test non-object value handling
        if (rng.Next(0, 10) == 0)
        {
            return "_datadog";
        }

        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789_-";
        var len = rng.Next(1, 12);
        var buf = new char[len];
        for (int i = 0; i < len; i++)
        {
            buf[i] = chars[rng.Next(chars.Length)];
        }

        return new string(buf);
    }

    private static string RandomString(Random rng)
    {
        var sb = new StringBuilder();
        var len = rng.Next(0, 10);
        for (int i = 0; i < len; i++)
        {
            sb.Append(StringPieces[rng.Next(StringPieces.Length)]);
        }

        return sb.ToString();
    }

    private static void AssertNoCarrierObject(string result, long seed, int iteration, string original)
    {
        // Find "_datadog" in result; if found, the next non-ws char must NOT be ':'
        // followed by '{' (which would mean it's an object-valued carrier).
        int pos = 0;
        while ((pos = result.IndexOf("\"_datadog\"", pos, StringComparison.Ordinal)) >= 0)
        {
            int after = pos + "\"_datadog\"".Length;
            while (after < result.Length && result[after] is ' ' or '\t' or '\n' or '\r')
            {
                after++;
            }

            if (after < result.Length && result[after] == ':')
            {
                int valStart = after + 1;
                while (valStart < result.Length && result[valStart] is ' ' or '\t' or '\n' or '\r')
                {
                    valStart++;
                }

                if (valStart < result.Length && result[valStart] == '{')
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Carrier object found in output. seed={seed}, iteration={iteration}\ninput={original}\nresult={result}");
                }
            }

            pos += 1;
        }
    }
}
#endif
