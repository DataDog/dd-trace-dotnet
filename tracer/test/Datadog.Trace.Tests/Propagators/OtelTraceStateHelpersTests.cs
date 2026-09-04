// <copyright file="OtelTraceStateHelpersTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
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
        [InlineData("rv:zzzzzz", null)]
        // rv must contain exactly 14 lowercase hexadecimal digits.
        [InlineData("rv:ef284ace7a91e", null)]
        [InlineData("rv:123456789abcdef1", null)]
        [InlineData("rv:", null)]
        public void ExtractRv_ReturnsValueOrNull(string? raw, ulong? expected)
        {
            OtelTraceStateHelpers.ExtractRv(raw).Should().Be(expected);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("", null, null)]
        [InlineData("rv:ef284ace7a91e1", 0xef284ace7a91e1UL, null)]
        [InlineData("th:0", null, 0UL)]
        [InlineData("th:e6666666666668", null, 0xe6666666666668UL)]
        [InlineData("unknownkey:whatever", null, null)]
        [InlineData("rv:zz;th:zz", null, null)]
        [InlineData("rv:ef284ace7a91e1;th:zz", 0xef284ace7a91e1UL, null)]
        [InlineData("rv:zz;th:e6666666666668", null, 0xe6666666666668UL)]
        [InlineData("foo:bar;rv:zz;th:e6666666666668;baz:qux", null, 0xe6666666666668UL)]
        // rv values use lowercase hexadecimal digits.
        [InlineData("rv:EF284ACE7A91E1;foo:bar", null, null)]
        // th must contain no more than 14 lowercase hexadecimal digits.
        [InlineData("th:123456789abcdef;foo:bar", null, null)]
        [InlineData("rv:;th;foo:bar", null, null)]
        public void Parse_RemovesOnlyMalformedRvAndTh(string? raw, ulong? expectedRv, ulong? expectedTh)
        {
            var otelTraceState = OtelTraceState.Parse(raw);
            otelTraceState.RandomValue.Should().Be(expectedRv);
            otelTraceState.Threshold.Should().Be(expectedTh);
        }

        [Theory]
        [InlineData(null, null, null, "")]
        [InlineData("", null, null, "")]
        [InlineData(null, 0x1UL, null, "rv:00000000000001")]
        [InlineData(null, 0xef284ace7a91e1UL, null, "rv:ef284ace7a91e1")]
        [InlineData(null, null, 0xe6666666666668UL, "th:e6666666666668")]
        [InlineData(null, 0xef284ace7a91e1UL, 0xe6666666666668UL, "rv:ef284ace7a91e1;th:e6666666666668")]
        [InlineData(null, null, 0x100UL, "th:000000000001")]
        [InlineData(null, null, 0UL, "th:0")]
        [InlineData("foo:bar;rv:1;th:2", 0xef284ace7a91e1UL, 0xe6666666666668UL, "rv:ef284ace7a91e1;th:e6666666666668;foo:bar")]
        [InlineData("rv:zzzz;th:2;foo:bar", 0xef284ace7a91e1UL, null, "rv:ef284ace7a91e1;foo:bar")]
        [InlineData("foo:bar", null, null, "foo:bar")]
        public void SetRvTh_RewritesRvAndThInPlace(string? raw, ulong? rv, ulong? th, string? expected)
        {
            var sb = new StringBuilder();
            OtelTraceStateHelpers.SetRvTh(sb, raw, rv, th);
            sb.ToString().Should().Be(expected);
        }

        [Fact]
        public void SetRvTh_ThrowsWhenRvExceeds56Bits()
        {
            var sb = new StringBuilder();
            FluentActions.Invoking(() => OtelTraceStateHelpers.SetRvTh(sb, null, 1UL << 56, null))
                         .Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
