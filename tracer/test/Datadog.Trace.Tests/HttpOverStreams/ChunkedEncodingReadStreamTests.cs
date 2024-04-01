// <copyright file="ChunkedEncodingReadStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using static Datadog.Trace.Util.EncodingHelpers;

namespace Datadog.Trace.Tests.HttpOverStreams;

public class ChunkedEncodingReadStreamTests
{
    public static readonly int[] ChunkSizes = [5, 9, 50, 127, 255, 256, 257, 1053, 4095, 4096, 4097, 8192,];
    private static readonly int[] Offsets = [0, 1, 5, 15];
    private static readonly int[] BufferSizesRelativeToChunk = [-100, -10, -1, 0, 1, 10, 100];
    private static readonly string[] LineEndings = ["\r\n", "\n"]; // only \r\n is compliant, but play it safe

    public static IEnumerable<object[]> GetChunkAndBufferSizes()
        => from chunkSize in ChunkSizes
           from offset in Offsets
           from bufferRelativeSize in BufferSizesRelativeToChunk
           from lineEnding in LineEndings
           let bufferSize = chunkSize + bufferRelativeSize
           where bufferSize - offset > 0
           select new object[] { bufferSize, chunkSize, offset, lineEnding };

    [Theory]
    [InlineData(0, "0")]
    [InlineData(15, "F")]
    [InlineData(12345, "3039")]
    [InlineData(int.MaxValue, "7FFFFFFF")]
    public void ParseChunkHexString_GivesCorrectOutput(int chunkSize, string headerBytes)
    {
        // doing a bigger buffer with a somewhat random offset
        var offset = 3;
        var buffer = new byte[20];
        var bytesWritten = Utf8NoBom.GetBytes(headerBytes, 0, headerBytes.Length, buffer, offset);

        var parsedValue = (int)ChunkedEncodingReadStream.ParseChunkHexString(buffer, offset, bytesWritten);
        parsedValue.Should().Be(chunkSize);
    }

    [Theory]
    [InlineData(12345, "3039;")]
    [InlineData(12345, "3039;test")]
    [InlineData(12345, "3039;test1;test2;")]
    [InlineData(12345, "3039;test1 x=123;test2;")]
    public void ParseChunkHexString_IgnoresExtensions_GivesCorrectOutput(int chunkSize, string headerBytes)
    {
        // doing a bigger buffer with a somewhat random offset
        var offset = 3;
        var buffer = new byte[128];
        var bytesWritten = Utf8NoBom.GetBytes(headerBytes, 0, headerBytes.Length, buffer, offset);

        var parsedValue = (int)ChunkedEncodingReadStream.ParseChunkHexString(buffer, offset, bytesWritten);
        parsedValue.Should().Be(chunkSize);
    }

    [Theory]
    [MemberData(nameof(GetChunkAndBufferSizes))]
    public async Task ChunkedEncodingReadStream_IsReadCorrectly(int bufferSize, int chunkSize, int offset, string lineEnding)
    {
        const int chunkCount = 10;
        var hexChunkSize = chunkSize.ToString("X");
        var chars = GetChunk(chunkSize);
        var expected = string.Join(string.Empty, Enumerable.Repeat(chars, chunkCount));

        var sb = new StringBuilder();
        for (var i = 0; i < chunkCount; i++)
        {
            sb.Append(hexChunkSize)
              .Append(lineEnding)
              .Append(chars)
              .Append(lineEnding);
        }

        // final chunk
        sb.Append('0')
          .Append(lineEnding)
          .Append(lineEnding);

        var input = Utf8NoBom.GetBytes(sb.ToString());

        using var ms = new MemoryStream(input);

        var totalBytesRead = await ReadChunkedData(bufferSize, offset, ms, sb);

        using var scope = new AssertionScope();
        totalBytesRead.Should().Be(chunkSize * 10);
        sb.ToString().Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(GetChunkAndBufferSizes))]
    public async Task ChunkedEncodingReadStream_AndWriteStream_CanRoundTrip(int bufferSize, int chunkSize, int offset, string lineEnding)
    {
        _ = lineEnding; // not used, stop xunit complaining
        const int chunkCount = 10;

        var chars = GetChunk(chunkSize);
        var charsAsByteArray = Utf8NoBom.GetBytes(chars);
        var expected = string.Join(string.Empty, Enumerable.Repeat(chars, chunkCount));

        using var ms = new MemoryStream();
        using var chunkWriter = new ChunkedEncodingWriteStream(ms);

        for (var i = 0; i < chunkCount; i++)
        {
            await chunkWriter.WriteAsync(charsAsByteArray, 0, charsAsByteArray.Length);
        }

        await chunkWriter.FinishAsync();

        // reset to start
        ms.Position = 0;
        var sb = new StringBuilder();

        var totalBytesRead = await ReadChunkedData(bufferSize, offset, ms, sb);

        using var scope = new AssertionScope();
        totalBytesRead.Should().Be(charsAsByteArray.Length * 10);
        sb.ToString().Should().Be(expected);
    }

    internal static string GetChunk(int chunkSize)
        => string.Join(
            string.Empty,
            Enumerable.Range(0, chunkSize).Select(x => (x % 16).ToString("x")));

    private static async Task<int> ReadChunkedData(int bufferSize, int offset, MemoryStream ms, StringBuilder sb)
    {
        var streamEncoding = new ChunkedEncodingReadStream(ms);

        int totalBytesRead = 0;
        int bytesRead = 0;

        var destinationBuffer = new byte[bufferSize];
        var length = bufferSize - offset;
        sb.Clear();

        do
        {
            bytesRead = await streamEncoding.ReadAsync(destinationBuffer, offset, length);
            totalBytesRead += bytesRead;

            // this is obviously inefficient, but don't care
            sb.Append(Utf8NoBom.GetString(destinationBuffer, offset, bytesRead));
        }
        while (bytesRead > 0);

        return totalBytesRead;
    }
}
