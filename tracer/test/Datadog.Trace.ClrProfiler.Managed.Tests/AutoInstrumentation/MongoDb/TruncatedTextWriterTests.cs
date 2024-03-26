// <copyright file="TruncatedTextWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.MongoDb;

public class TruncatedTextWriterTests
{
    [Fact]
    public void Truncates_Strings()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write("Test ");
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith("Test Test Test ");
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

    [Fact]
    public void Truncates_FormatStrings()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write("Format {0:E} ", 1_000_000);
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith("Format 1.000000E+006 Format 1.000000E+006");
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

    [Fact]
    public void Truncates_StringBuilders()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            var sb2 = new StringBuilder();
            sb2.Append("Test ");
            sw.Write(sb2);
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith("Test Test Test ");
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

#if NETCOREAPP
    [Theory]
    [InlineData(1, "TTTTTT")]
    [InlineData(2, "TeTeTe")]
    [InlineData(5, "Test Test ")]
    public void Truncates_ReadOnlySpan(int spanLength, string startsWith)
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write("Test ".AsSpan().Slice(0, spanLength));
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith(startsWith);
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }
#endif

    [Fact]
    public void Truncates_Char()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write('c');
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith("cccccccccc");
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

    [Theory]
    [InlineData(1, "TTTTT")]
    [InlineData(2, "TeTeTe")]
    [InlineData(4, "TestTestTest")]
    public void Truncates_CharArray(int charsToWrite, string startsWith)
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write(new[] { 'T', 'e', 's', 't' }, 0, charsToWrite);
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith(startsWith);
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

    [Theory]
    [InlineData(1, "11111")]
    [InlineData(10, "101010101")]
    [InlineData(10_000_000, "1000000010000000")]
    public void Truncates_Integers(int number, string startsWith)
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write(number);
        }

        var finalString = sw.ToString();
        finalString.Should().StartWith(startsWith);
        finalString.Length.Should().Be(TruncatedTextWriter.MaxLength);
    }

    [Fact]
    public void Handles_NullStrings()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedTextWriter(sb);
        for (var i = 0; i < 2 * TruncatedTextWriter.MaxLength; i++)
        {
            sw.Write((string)null);
        }

        var finalString = sw.ToString();
        finalString.Should().BeNullOrEmpty();
    }
}
