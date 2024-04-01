// <copyright file="ChunkedEncodingReadStream.HexHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers.Text;

namespace Datadog.Trace.HttpOverStreams;

internal partial class ChunkedEncodingReadStream
{
    // internal for testing
    internal static ulong ParseChunkHexString(byte[] buffer, int offset, int length)
    {
        // Try to format into our output buffer directly.
        if (TryParseUInt64FromUtf8(buffer, offset, length, out var value, out var bytesConsumed))
        {
            if (bytesConsumed != length)
            {
                ValidateChunkExtension(buffer, offset + bytesConsumed, length - bytesConsumed);
            }

            return value;
        }

        throw new Exception($"Invalid response chunk header: {BitConverter.ToString(buffer, offset, length)}");

        static void ValidateChunkExtension(byte[] buffer, int offset, int length)
        {
            // Until we see the ';' denoting the extension, the line after the chunk size
            // must contain only tabs and spaces.  After the ';', anything goes.
            for (var i = 0; i < length; i++)
            {
                var c = buffer[offset + i];
                if (c == ';')
                {
                    break;
                }

                if (c != ' ' && c != '\t')
                {
                    // not called out in the RFC, but WinHTTP allows it

                    throw new Exception($"Invalid response chunk, extension invalid: {BitConverter.ToString(buffer, offset, length)}");
                }
            }
        }
    }

    private static bool TryParseUInt64FromUtf8(byte[] source, int sourceOffset, int sourceLength, out ulong value, out int bytesConsumed)
#if NET6_0_OR_GREATER
            => System.Buffers.Text.Utf8Parser.TryParse(source.AsSpan(sourceOffset, sourceLength), out value, out bytesConsumed, 'X');
#else
    {
        // Essentially a clone of System.Buffers.Text.Utf8Parser.TryParse
        if (sourceLength < 1)
        {
            bytesConsumed = 0;
            value = default;
            return false;
        }

        byte nextCharacter;
        byte nextDigit;

        // Parse the first digit separately. If invalid here, we need to return false.
        nextCharacter = source[sourceOffset];
        nextDigit = (byte)HexConverter.FromChar(nextCharacter);
        if (nextDigit == 0xFF)
        {
            bytesConsumed = 0;
            value = default;
            return false;
        }

        ulong parsedValue = nextDigit;

        if (sourceLength <= ParserHelpers.Int64OverflowLengthHex)
        {
            // Length is less than or equal to Parsers.Int64OverflowLengthHex; overflow is not possible
            for (var index = 1; index < sourceLength; index++)
            {
                nextCharacter = source[sourceOffset + index];
                nextDigit = (byte)HexConverter.FromChar(nextCharacter);
                if (nextDigit == 0xFF)
                {
                    bytesConsumed = index;
                    value = parsedValue;
                    return true;
                }

                parsedValue = (parsedValue << 4) + nextDigit;
            }
        }
        else
        {
            // Length is greater than Parsers.Int64OverflowLengthHex; overflow is only possible after Parsers.Int64OverflowLengthHex
            // digits. There may be no overflow after Parsers.Int64OverflowLengthHex if there are leading zeroes.
            for (var index = 1; index < ParserHelpers.Int64OverflowLengthHex; index++)
            {
                nextCharacter = source[sourceOffset + index];
                nextDigit = (byte)HexConverter.FromChar(nextCharacter);
                if (nextDigit == 0xFF)
                {
                    bytesConsumed = index;
                    value = parsedValue;
                    return true;
                }

                parsedValue = (parsedValue << 4) + nextDigit;
            }

            for (var index = ParserHelpers.Int64OverflowLengthHex; index < sourceLength; index++)
            {
                nextCharacter = source[sourceOffset + index];
                nextDigit = (byte)HexConverter.FromChar(nextCharacter);
                if (nextDigit == 0xFF)
                {
                    bytesConsumed = index;
                    value = parsedValue;
                    return true;
                }

                // If we try to append a digit to anything larger than ulong.MaxValue / 0x10, there will be overflow
                if (parsedValue > ulong.MaxValue / 0x10)
                {
                    bytesConsumed = 0;
                    value = default;
                    return false;
                }

                parsedValue = (parsedValue << 4) + nextDigit;
            }
        }

        bytesConsumed = sourceLength;
        value = parsedValue;
        return true;
    }
#endif
}
