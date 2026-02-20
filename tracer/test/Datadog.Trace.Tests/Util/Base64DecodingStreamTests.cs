// <copyright file="Base64DecodingStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class Base64DecodingStreamTests
    {
        [Fact]
        public void Constructor_NullInput_Throws()
        {
            Action act = () => new Base64DecodingStream(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Read_EmptyString_ReturnsZeroBytes()
        {
            using var stream = new Base64DecodingStream(string.Empty);
            var buffer = new byte[16];

            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            bytesRead.Should().Be(0);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(100)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(1000)]
        [InlineData(3072)]
        [InlineData(4096)]
        [InlineData(8192)]
        public void Read_DecodesCorrectly_ForVariousInputSizes(int byteCount)
        {
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytes(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData(1)]   // 1 byte → 2 base64 chars + 2 padding
        [InlineData(2)]   // 2 bytes → 3 base64 chars + 1 padding
        [InlineData(3)]   // 3 bytes → 4 base64 chars, no padding
        public void Read_HandlesBase64Padding_Correctly(int byteCount)
        {
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytes(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData(100, 3)]
        [InlineData(100, 7)]
        [InlineData(100, 16)]
        [InlineData(100, 64)]
        [InlineData(100, 128)]
        [InlineData(5000, 3)]
        [InlineData(5000, 13)]
        [InlineData(5000, 256)]
        [InlineData(5000, 1024)]
        [InlineData(5000, 4096)]
        [InlineData(5000, 8192)]
        public void Read_ProducesCorrectResult_WithDifferentReadBufferSizes(int byteCount, int readBufferSize)
        {
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytesWithBufferSize(stream, readBufferSize);

            decoded.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData(3072)]
        [InlineData(3073)]
        [InlineData(6144)]
        [InlineData(6145)]
        public void Read_HandlesChunkBoundaries_Correctly(int byteCount)
        {
            // 3072 decoded bytes = 4096 base64 chars = exactly one chunk
            // 3073 decoded bytes spans into a second chunk
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytes(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Fact]
        public void Read_AllZeroBytes_DecodesCorrectly()
        {
            var originalBytes = new byte[256];
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytes(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Fact]
        public void Read_AllOnesBytes_DecodesCorrectly()
        {
            var originalBytes = Enumerable.Repeat((byte)0xFF, 256).ToArray();
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = ReadAllBytes(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Theory]
        [InlineData("!!!")]
        [InlineData("not-base64")]
        [InlineData("====")]
        public void Read_InvalidBase64_ThrowsFormatException(string invalidBase64)
        {
            using var stream = new Base64DecodingStream(invalidBase64);
            var buffer = new byte[64];

#pragma warning disable CA2022 // Avoid exact read (.NET 10+)
            Action act = () => stream.Read(buffer, 0, buffer.Length);
#pragma warning restore CA2022

            act.Should().Throw<FormatException>();
        }

#if NETCOREAPP3_1_OR_GREATER
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void Read_BufferTooSmall_ThrowsArgumentException(int bufferSize)
        {
            using var stream = new Base64DecodingStream("AQID"); // decodes to [1, 2, 3]
            var buffer = new byte[bufferSize];

#pragma warning disable CA2022 // Avoid exact read (.NET 10+)
            Action act = () => stream.Read(buffer, 0, buffer.Length);
#pragma warning restore CA2022

            act.Should().Throw<ArgumentException>();
        }
#endif

        [Theory]
        [InlineData(100, 128)]
        [InlineData(1000, 256)]
        [InlineData(5000, 1024)]
        public void Read_ViaStreamReader_ProducesCorrectUtf8String(int stringLength, int streamReaderBufferSize)
        {
            // This tests the primary intended use case: base64 → stream → StreamReader → UTF-8 string
            var originalString = new string('A', stringLength / 2) + new string('Z', stringLength - (stringLength / 2));
            var originalBytes = Encoding.UTF8.GetBytes(originalString);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, streamReaderBufferSize);
            var result = reader.ReadToEnd();

            result.Should().Be(originalString);
        }

        [Theory]
        [InlineData("""{"signed":{"version":1,"spec_version":"1.0.0"},"signatures":[]}""")]
        [InlineData("Hello \u00e9\u00e8\u00ea \u4e16\u754c \ud83d\ude00")]
        public void Read_DecodesCorrectly(string expected)
        {
            // Simulates the TufRootBase64Converter use case
            var bytes = Encoding.UTF8.GetBytes(expected);
            var base64 = Convert.ToBase64String(bytes);

            using var stream = new Base64DecodingStream(base64);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var result = reader.ReadToEnd();

            result.Should().Be(expected);
        }

        [Fact]
        public void Read_AfterEndOfStream_ReturnsZero()
        {
            var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });

            using var stream = new Base64DecodingStream(base64);
            var buffer = new byte[64];

            // First read should return all bytes
            var firstRead = stream.Read(buffer, 0, buffer.Length);
            firstRead.Should().Be(3);

            // Subsequent reads should return 0
            var secondRead = stream.Read(buffer, 0, buffer.Length);
            secondRead.Should().Be(0);

            var thirdRead = stream.Read(buffer, 0, buffer.Length);
            thirdRead.Should().Be(0);
        }

        [Fact]
        public void Read_ZeroCount_ReturnsZero()
        {
            var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });

            using var stream = new Base64DecodingStream(base64);
            var buffer = new byte[64];

            var bytesRead = stream.Read(buffer, 0, 0);

            bytesRead.Should().Be(0);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(5000)]
        public async Task ReadAsync_ArrayOverload_DecodesCorrectly(int byteCount)
        {
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            using var stream = new Base64DecodingStream(base64);
            var decoded = await ReadAllBytesAsync(stream);

            decoded.Should().Equal(originalBytes);
        }

        [Fact]
        public async Task ReadAsync_CancelledToken_ThrowsOperationCancelledException()
        {
            using var stream = new Base64DecodingStream("AQID");
            var buffer = new byte[64];
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Theory]
        [InlineData(100)]
        [InlineData(5000)]
        public void Read_MatchesConvertFromBase64String(int byteCount)
        {
            // Verify our stream produces the exact same bytes as Convert.FromBase64String
            var originalBytes = CreateSequentialBytes(byteCount);
            var base64 = Convert.ToBase64String(originalBytes);

            var expected = Convert.FromBase64String(base64);

            using var stream = new Base64DecodingStream(base64);
            var actual = ReadAllBytes(stream);

            actual.Should().Equal(expected);
        }

        private static byte[] CreateSequentialBytes(int count)
        {
            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                bytes[i] = (byte)(i % 256);
            }

            return bytes;
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static byte[] ReadAllBytesWithBufferSize(Stream stream, int bufferSize)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }

            return ms.ToArray();
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            var buffer = new byte[256];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }

            return ms.ToArray();
        }
    }
}
