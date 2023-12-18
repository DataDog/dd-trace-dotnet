// <copyright file="ChunkedEncodingWriteStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Text;
using Datadog.Trace.HttpOverStreams.ChunkedEncoding;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams;

public class ChunkedEncodingWriteStreamTests
{
    [Theory]
    [InlineData(0, 1, "0")]
    [InlineData(15, 1, "F")]
    [InlineData(12345, 4, "3039")]
    [InlineData(int.MaxValue, 8, "7FFFFFFF")]
    public void WriteChunkedEncodingHeaderToBuffer_GivesCorrectOutput(int count, int bytes, string expected)
    {
        var buffer = new byte[8];
        var bytesWritten = ChunkedEncodingWriteStream.WriteChunkedEncodingHeaderToBuffer(buffer, count);
        var result = Encoding.UTF8.GetString(buffer, index: 0, count: bytesWritten);

        using var x = new AssertionScope();
        bytesWritten.Should().Be(bytes);
        result.Should().Be(expected);
    }
}
