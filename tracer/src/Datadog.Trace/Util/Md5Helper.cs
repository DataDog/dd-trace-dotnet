// <copyright file="Md5Helper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Util;

internal static class Md5Helper
{
#if NETCOREAPP
    /// <summary>
    /// Compute the MD5 hash of the input by first converting it to UTF8.
    /// The result is stored in <paramref name="buffer"/> which must have a length of at least 16 bytes
    /// e.g. <c>Span&lt;byte&gt; hash = stackalloc byte[16];</c>
    /// </summary>
    /// <returns>The number of bytes written into the buffer, which is always 16</returns>
    public static int ComputeMd5Hash(string input, Span<byte> buffer)
    {
        System.Diagnostics.Debug.Assert(buffer.Length == 16, "buffer.Length must always be at least 16");
        // 1. Encode input to UTF8
        // arbitrary threshold for stackalloc
        var maxInputSize = EncodingHelpers.Utf8NoBom.GetMaxByteCount(input.Length);
        var inputBuffer = maxInputSize <= 512
                              ? stackalloc byte[512]
                              : new byte[maxInputSize];

        var encodeCount = EncodingHelpers.Utf8NoBom.GetBytes(input, inputBuffer);
        var encodedInput = inputBuffer.Slice(0, encodeCount);

        // 2. Take MD5 of encoded input
#if NET6_0_OR_GREATER
        var byteCount = MD5.HashData(encodedInput, buffer);
#else
        using var md5 = MD5.Create();
        if (!md5.TryComputeHash(encodedInput, buffer, out var byteCount))
        {
            // Something very wrong and weird has happened
            // we have to use TryComputeHash because ComputeHash has no readonly overloads
            ThrowHelper.ThrowArgumentException($"Error computing MD5 hash for {input}: ");
        }
#endif
        System.Diagnostics.Debug.Assert(byteCount == 16, "we should always encode 16 bytes");
        return byteCount;
    }
#else
    /// <summary>
    /// Compute the MD5 hash of the input by first converting it to UTF8.
    /// The result is returned from the method
    /// </summary>
    /// <returns>The MD5 hash of the encoded input</returns>
    public static byte[] ComputeMd5Hash(string input)
    {
        // 1. Encode input to UTF8
        // arbitrary threshold for stackalloc
        var maxInputSize = EncodingHelpers.Utf8NoBom.GetMaxByteCount(input.Length);
        byte[]? pooledArray = null;
        try
        {
            pooledArray = ArrayPool<byte>.Shared.Rent(maxInputSize);
            var encodeCount = EncodingHelpers.Utf8NoBom.GetBytes(input, charIndex: 0, charCount: input.Length, pooledArray, byteIndex: 0);

            // 2. Take MD5 of encoded input
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(pooledArray, offset: 0, count: encodeCount);
            return hash;
        }
        finally
        {
            if (pooledArray is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }
#endif
}
