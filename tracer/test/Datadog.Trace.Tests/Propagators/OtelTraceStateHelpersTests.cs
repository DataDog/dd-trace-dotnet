// <copyright file="OtelTraceStateHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Propagators
{
    public class OtelTraceStateHelpersTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("th:e6666666666668", null)]
        [InlineData("rv:ef284ace7a91e1", 0xef284ace7a91e1UL)]
        [InlineData("rv:ef284ace7a91e1;th:e6666666666668", 0xef284ace7a91e1UL)]
        [InlineData("th:e6666666666668;rv:ef284ace7a91e1", 0xef284ace7a91e1UL)]
        [InlineData("foo:bar;rv:1;baz:qux", null)]
        [InlineData("rv:zzzzzz", null)] // not hex -> malformed -> null
        [InlineData("rv:123456789abcdef1", null)] // 15 hex digits -> too long -> malformed -> null
        [InlineData("rv:", null)] // empty value -> malformed -> null
        public void ExtractRv_ReturnsValueOrNull(string? raw, ulong? expected)
        {
            OtelTraceStateHelpers.ExtractRv(raw).Should().Be(expected);
        }

        [Theory]
        // no rv, no th, no other items -> null
        [InlineData(null, null, null, null)]
        [InlineData("", null, null, null)]
        // rv only
        [InlineData(null, 0xef284ace7a91e1UL, null, "rv:ef284ace7a91e1")]
        // th only, no trailing zeros to trim
        [InlineData(null, null, 0xe6666666666668UL, "th:e6666666666668")]
        // both rv and th, rv first
        [InlineData(null, 0xef284ace7a91e1UL, 0xe6666666666668UL, "rv:ef284ace7a91e1;th:e6666666666668")]
        // th with trailing zero nibbles trimmed
        [InlineData(null, null, 0x100UL, "th:000000000001")]
        // existing rv/th replaced, unrelated sub-key preserved in original order
        [InlineData("foo:bar;rv:1;th:2", 0xef284ace7a91e1UL, 0xe6666666666668UL, "rv:ef284ace7a91e1;th:e6666666666668;foo:bar")]
        // malformed existing rv/th still stripped even though we're not re-deriving them
        [InlineData("rv:zzzz;th:2;foo:bar", 0xef284ace7a91e1UL, null, "rv:ef284ace7a91e1;foo:bar")]
        // rv/th both null, unrelated sub-key preserved
        [InlineData("foo:bar", null, null, "foo:bar")]
        public void SetRvTh_RewritesRvAndThInPlace(string? raw, ulong? rv, ulong? th, string? expected)
        {
            OtelTraceStateHelpers.SetRvTh(raw, rv, th).Should().Be(expected);
        }

        [Fact]
        public void SetRvTh_ThZero_EmitsSingleZeroDigit()
        {
            OtelTraceStateHelpers.SetRvTh(null, rv: null, th: 0UL).Should().Be("th:0");
        }
    }
}
