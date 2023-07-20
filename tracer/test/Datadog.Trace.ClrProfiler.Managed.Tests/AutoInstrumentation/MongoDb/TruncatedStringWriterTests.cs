// <copyright file="TruncatedStringWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.MongoDb;

public class TruncatedStringWriterTests
{
    [Fact]
    public void Truncates_Strings()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedStringWriter(sb);
        for (var i = 0; i < 2 * TruncatedStringWriter.MaxLength; i++)
        {
            sw.Write("Test ");
        }

        var finalString = sw.ToString();
        finalString.Length.Should().Be(TruncatedStringWriter.MaxLength);
    }

    [Fact]
    public void Truncates_Char()
    {
        var sb = new StringBuilder();
        using var sw = new TruncatedStringWriter(sb);
        for (var i = 0; i < 2 * TruncatedStringWriter.MaxLength; i++)
        {
            sw.Write('c');
        }

        var finalString = sw.ToString();
        finalString.Length.Should().Be(TruncatedStringWriter.MaxLength);
    }
}
