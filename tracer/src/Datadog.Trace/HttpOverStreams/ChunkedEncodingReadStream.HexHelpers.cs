// <copyright file="ChunkedEncodingReadStream.HexHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.HttpOverStreams;

internal partial class ChunkedEncodingReadStream
{
    [TestingAndPrivateOnly]
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
            => Utf8Parser.TryParse(source.AsSpan(sourceOffset, sourceLength), out value, out bytesConsumed, 'X');
}
