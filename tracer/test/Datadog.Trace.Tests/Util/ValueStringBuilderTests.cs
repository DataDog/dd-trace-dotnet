// <copyright file="ValueStringBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

// Based on tests from https://github.com/dotnet/runtime/blob/b1e550cccc539b438a19f45816e8c5030ebb89db/src/libraries/Common/tests/Tests/System/Text/ValueStringBuilderTests.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Datadog.Trace.Util;
using FluentAssertions;

namespace Datadog.Trace.Tests.Util;

using Xunit;

public class ValueStringBuilderTests
{
    [Fact]
    public void Ctor_Default_CanAppend()
    {
        var vsb = default(ValueStringBuilder);
        vsb.Length.Should().Be(0);

        vsb.Append('a');
        vsb.Length.Should().Be(1);
        vsb.ToString().Should().Be("a");
    }

    [Fact]
    public void Ctor_Span_CanAppend()
    {
        var vsb = new ValueStringBuilder(new char[1]);
        vsb.Length.Should().Be(0);

        vsb.Append('a');
        vsb.Length.Should().Be(1);
        vsb.ToString().Should().Be("a");
    }

    [Fact]
    public void Ctor_InitialCapacity_CanAppend()
    {
        var vsb = new ValueStringBuilder(1);
        vsb.Length.Should().Be(0);

        vsb.Append('a');
        vsb.Length.Should().Be(1);
        vsb.ToString().Should().Be("a");
    }

    [Fact]
    public void Append_Char_MatchesStringBuilder()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();
        for (int i = 1; i <= 100; i++)
        {
            sb.Append((char)i);
            vsb.Append((char)i);
        }

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Fact]
    public void Append_String_MatchesStringBuilder()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();
        for (int i = 1; i <= 100; i++)
        {
            string s = i.ToString();
            sb.Append(s);
            vsb.Append(s);
        }

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Theory]
    [InlineData(0, 4 * 1024 * 1024)]
    [InlineData(1025, 4 * 1024 * 1024)]
    [InlineData(3 * 1024 * 1024, 6 * 1024 * 1024)]
    public void Append_String_Large_MatchesStringBuilder(int initialLength, int stringLength)
    {
        var sb = new StringBuilder(initialLength);
        var vsb = new ValueStringBuilder(new char[initialLength]);

        string s = new string('a', stringLength);
        sb.Append(s);
        vsb.Append(s);

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Theory]
    [InlineData(0, 4 * 1024 * 1024)]
    [InlineData(1025, 4 * 1024 * 1024)]
    [InlineData(3 * 1024 * 1024, 6 * 1024 * 1024)]
    public void AppendLowerInvariant_String_Large_MatchesStringBuilder(int initialLength, int stringLength)
    {
        var sb = new StringBuilder(initialLength);
        var vsb = new ValueStringBuilder(new char[initialLength]);

        string s = new string('A', stringLength);
        sb.Append(s);
        vsb.AppendAsLowerInvariant(s);

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString().ToLowerInvariant());
    }

    [Fact]
    public void Append_CharInt_MatchesStringBuilder()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();
        for (int i = 1; i <= 100; i++)
        {
            sb.Append((char)i, i);
            vsb.Append((char)i, i);
        }

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Fact]
    public void AppendSpan_DataAppendedCorrectly()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();

        for (int i = 1; i <= 1000; i++)
        {
            string s = i.ToString();

            sb.Append(s);

            Span<char> span = vsb.AppendSpan(s.Length);
            vsb.Length.Should().Be(sb.Length);

            s.AsSpan().CopyTo(span);
        }

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Fact]
    public void Insert_IntCharInt_MatchesStringBuilder()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();
        var rand = new Random(42);

        for (int i = 1; i <= 100; i++)
        {
            int index = rand.Next(sb.Length);
            sb.Insert(index, new string((char)i, 1), i);
            vsb.Insert(index, (char)i, i);
        }

        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Fact]
    public void AsSpan_ReturnsCorrectValue_DoesntClearBuilder()
    {
        var sb = new StringBuilder();
        var vsb = new ValueStringBuilder();

        for (int i = 1; i <= 100; i++)
        {
            string s = i.ToString();
            sb.Append(s);
            vsb.Append(s);
        }

        var resultString = new string(vsb.AsSpan());
        resultString.Should().Be(sb.ToString());

        sb.Length.Should().NotBe(0);
        vsb.Length.Should().Be(sb.Length);
        vsb.ToString().Should().Be(sb.ToString());
    }

    [Fact]
    public void ToString_ClearsBuilder_ThenReusable()
    {
        const string Text1 = "test";
        var vsb = new ValueStringBuilder();

        vsb.Append(Text1);
        vsb.Length.Should().Be(Text1.Length);

        string s = vsb.ToString();
        s.Should().Be(Text1);

        vsb.Length.Should().Be(0);
        vsb.ToString().Should().BeEmpty();

        const string Text2 = "another test";
        vsb.Append(Text2);
        vsb.Length.Should().Be(Text2.Length);
        vsb.ToString().Should().Be(Text2);
    }

    [Fact]
    public void Dispose_ClearsBuilder_ThenReusable()
    {
        const string Text1 = "test";
        var vsb = new ValueStringBuilder();

        vsb.Append(Text1);
        vsb.Length.Should().Be(Text1.Length);

        vsb.Dispose();

        vsb.Length.Should().Be(0);
        vsb.ToString().Should().BeEmpty();

        const string Text2 = "another test";
        vsb.Append(Text2);
        vsb.Length.Should().Be(Text2.Length);
        vsb.ToString().Should().Be(Text2);
    }

    [Fact]
    public void Indexer()
    {
        const string Text1 = "foobar";
        var vsb = new ValueStringBuilder();

        vsb.Append(Text1);

        vsb[3].Should().Be('b');
        vsb[3] = 'c';
        vsb[3].Should().Be('c');
        vsb.Dispose();
    }

    [Fact]
    public void EnsureCapacity_IfRequestedCapacityWins()
    {
        // Note: constants used here may be dependent on minimal buffer size
        // the ArrayPool is able to return.
        var builder = new ValueStringBuilder(stackalloc char[32]);

        builder.EnsureCapacity(65);

        builder.Capacity.Should().Be(128);
    }

    [Fact]
    public void EnsureCapacity_IfBufferTimesTwoWins()
    {
        var builder = new ValueStringBuilder(stackalloc char[32]);

        builder.EnsureCapacity(33);

        builder.Capacity.Should().Be(64);
        builder.Dispose();
    }

    [Fact]
    public void EnsureCapacity_NoAllocIfNotNeeded()
    {
        // Note: constants used here may be dependent on minimal buffer size
        // the ArrayPool is able to return.
        var builder = new ValueStringBuilder(stackalloc char[64]);

        builder.EnsureCapacity(16);

        builder.Capacity.Should().Be(64);
        builder.Dispose();
    }
}
#endif
