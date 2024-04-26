// <copyright file="SpanCharSplitterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Datadog.Trace.Tests.Util;

public class SpanCharSplitterTests
{
    [Fact]
    public void SplitSpan()
    {
        var input = "hello;world;;a";
        var enumerator = input.SplitIntoSpans(';').GetEnumerator();

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
        var input = "hello;world;;a";
        var enumerator = input.SplitIntoSpans(';', 3).GetEnumerator();

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
    public void Empty()
    {
        var input = string.Empty;
        var enumerator = input.SplitIntoSpans(';', 3).GetEnumerator();

        // Empty
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        // end
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void Null()
    {
        string input = null;

        // ReSharper disable once AssignNullToNotNullAttribute
        Invoking(() => input.SplitIntoSpans(';')).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NoSeparator()
    {
        var input = "hello world";
        var enumerator = input.SplitIntoSpans(';').GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(11);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo("hello world");

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void EmptyEnding()
    {
        var input = "hello;world;;";
        var enumerator = input.SplitIntoSpans(';').GetEnumerator();

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

        // ;
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(12);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        // ;
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(13);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        // end
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void EmptyString()
    {
        var input = string.Empty;
        var enumerator = input.SplitIntoSpans(';').GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Length.Should().Be(0);
        enumerator.Current.StartIndex.Should().Be(0);
        enumerator.Current.AsSpan().ToArray().Should().BeEquivalentTo(string.Empty);

        enumerator.MoveNext().Should().BeFalse();
    }
}
