// <copyright file="SpanCharSplitterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class SpanCharSplitterTests
{
    [Fact]
    public void SplitSpan()
    {
        var separator = ";";
        var input = "hello;world;;a";

#if NETCOREAPP3_1_OR_GREATER
        var source = input.AsSpan();
#else
        var source = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(input);
#endif

        var enumerator = source.Split(separator).GetEnumerator();

        // hello
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(5);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("hello");

        // world
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(5);
        enumerator.Current.StartIndex.Should().Be(6);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("world");

        // empty
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(12);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        // a
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(1);
        enumerator.Current.StartIndex.Should().Be(13);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("a");

        // end
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Count()
    {
        var separator = ";";
        var input = "hello;world;;a";

#if NETCOREAPP3_1_OR_GREATER
        var source = input.AsSpan();
#else
        var source = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(input);
#endif

        var enumerator = source.Split(separator, 3).GetEnumerator();

        // hello
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(5);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("hello");

        // world
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(5);
        enumerator.Current.StartIndex.Should().Be(6);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("world");

        // ;a
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(2);
        enumerator.Current.StartIndex.Should().Be(12);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(";a");

        // end
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void NoSeparator()
    {
        var separator = ";";
        var input = "hello world";

#if NETCOREAPP3_1_OR_GREATER
        var source = input.AsSpan();
#else
        var source = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(input);
#endif

        var enumerator = source.Split(separator).GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(11);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("hello world");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void EmptyString()
    {
        var separator = ";";
        var input = string.Empty;

#if NETCOREAPP3_1_OR_GREATER
        var source = input.AsSpan();
#else
        var source = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(input);
#endif

        var enumerator = source.Split(separator).GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void EmptySeparator()
    {
        var separator = string.Empty;
        var input = "hello world";

#if NETCOREAPP3_1_OR_GREATER
        var source = input.AsSpan();
#else
        var source = Datadog.Trace.VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(input);
#endif

        try
        {
            _ = source.Split(separator);
        }
        catch (ArgumentException)
        {
            return;
        }

        Assert.Fail("No exception was thrown");
    }
}
