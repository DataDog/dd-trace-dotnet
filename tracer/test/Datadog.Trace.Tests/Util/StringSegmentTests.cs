// <copyright file="StringSegmentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class StringSegmentTests
    {
        [Fact]
        public void Slice_UsesOriginalString()
        {
            var segment = new StringSegment("abcdef", offset: 1, length: 4);

            var slice = segment.Slice(start: 1, length: 2);

            slice.Value.Should().BeSameAs(segment.Value);
            slice.Offset.Should().Be(2);
            slice.Length.Should().Be(2);
            slice.ToString().Should().Be("cd");
        }

#if NETCOREAPP
        [Fact]
        public void AsSpan_UsesSegmentBounds()
        {
            var segment = new StringSegment("abcdef", offset: 1, length: 4);

            segment.AsSpan().ToString().Should().Be("bcde");
        }

#endif
        [Theory]
        [InlineData("  value  ", "value")]
        [InlineData("\tvalue\r\n", "value")]
        [InlineData("\t\r\n", "")]
        [InlineData("value", "value")]
        public void Trim_UsesSegmentBounds(string value, string expected)
        {
            var segment = new StringSegment($"prefix{value}suffix", offset: 6, length: value.Length);

            var trimmed = segment.Trim();

            trimmed.Value.Should().BeSameAs(segment.Value);
            trimmed.ToString().Should().Be(expected);
        }

        [Theory]
        [InlineData("Ab", StringComparison.Ordinal, false)]
        [InlineData("Ab", StringComparison.OrdinalIgnoreCase, true)]
        [InlineData("ab", StringComparison.Ordinal, true)]
        public void Equals_UsesComparisonType(string value, StringComparison comparisonType, bool expected)
        {
            var segment = new StringSegment("abc", offset: 0, length: 2);

            segment.Equals(value, comparisonType).Should().Be(expected);
        }
    }
}
