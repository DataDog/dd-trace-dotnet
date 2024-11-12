// <copyright file="InitiallyBufferedStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Streams;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class InitiallyBufferedStreamTests
{
    private static readonly Random Rand = new();

    private static readonly int[] DataSizes = [0, 1, 2, 100, 127, 128, 129, 250, 254, 255, 256, 1000];
    // stream builder has a min buffer size of 128, so no sense trying anything smaller
    private static readonly int[] BufferSizes = [128, 129, 130];

    public static IEnumerable<object[]> GetSizes =>
        (from dataSize in DataSizes
         from bufferSize in BufferSizes
         select new object[] { dataSize, bufferSize });

    [Theory]
    [MemberData(nameof(GetSizes))]
    public void Read_ReturnsSameOutputAsReadingDirectlyFromInner(int dataLength, int bufferLength)
    {
        var rawBytes = GetSourceData(dataLength);

        using var innerStream = new MemoryStream(rawBytes);

        using var directStreamReader = new StreamReader(innerStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferLength, leaveOpen: true);
        var expected = directStreamReader.ReadToEnd();

        innerStream.Position = 0;
        var bufferedStream = new InitiallyBufferedStream(innerStream);
        using var bufferedStreamReader = new StreamReader(bufferedStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferLength, leaveOpen: true);
        var actual = bufferedStreamReader.ReadToEnd();

        actual.Should().Be(expected);
        // buffered encoded data should be expected
        var initialData = System.MemoryExtensions.AsSpan(rawBytes).Slice(0, Math.Min(InitiallyBufferedStream.MaxInitialBufferSize, dataLength));
        var initialString = Encoding.UTF8.GetString(initialData.ToArray());
        bufferedStream.GetBufferedContent().Should().Be(initialString);
    }

    [Theory]
    [MemberData(nameof(GetSizes))]
    public async Task ReadAsync_ReturnsSameOutputAsReadingDirectlyFromInner(int dataLength, int bufferLength)
    {
        var rawBytes = GetSourceData(dataLength);

        using var innerStream = new MemoryStream(rawBytes);

        using var directStreamReader = new StreamReader(innerStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferLength, leaveOpen: true);
        var expected = await directStreamReader.ReadToEndAsync();

        innerStream.Position = 0;
        var bufferedStream = new InitiallyBufferedStream(innerStream);
        using var bufferedStreamReader = new StreamReader(bufferedStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferLength, leaveOpen: true);
        var actual = await bufferedStreamReader.ReadToEndAsync();

        actual.Should().Be(expected);
        // buffered encoded data should be expected
        var initialData = new byte[Math.Min(InitiallyBufferedStream.MaxInitialBufferSize, dataLength)];
        Array.Copy(rawBytes, initialData, initialData.Length);
        var initialString = Encoding.UTF8.GetString(initialData.ToArray());
        bufferedStream.GetBufferedContent().Should().Be(initialString);
    }

    private static byte[] GetSourceData(int dataLength)
    {
        // using unicode here is a bit of a pain, even though we'd _like_ to
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789?{}][!\"£$%^&*(";

        var rawBytes = new byte[dataLength];
        for (var i = 0; i < dataLength; i++)
        {
            rawBytes[i] = (byte)chars[Rand.Next(chars.Length)];
        }

        return rawBytes;
    }
}
