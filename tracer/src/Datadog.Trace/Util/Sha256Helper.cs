// <copyright file="Sha256Helper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace Datadog.Trace.Util;

internal static class Sha256Helper
{
#if NETCOREAPP
    private const int Sha256HashSizeBytes = 32;
#endif

    public static string ComputeHashAsHexString(string input)
    {
        return ComputeHashAsHexString(input, EncodingHelpers.Utf8NoBom);
    }

    public static string ComputeHashAsHexString(string input, Encoding encoding)
    {
#if NETCOREAPP
        Span<byte> hash = stackalloc byte[Sha256HashSizeBytes];
        ComputeHash(input, encoding, hash);
        return HexString.ToHexString(hash);
#else
        return HexString.ToHexString(ComputeHash(input, encoding));
#endif
    }

#if NETCOREAPP
    public static int ComputeHash(string input, Span<byte> buffer)
    {
        return ComputeHash(input, EncodingHelpers.Utf8NoBom, buffer);
    }

    private static int ComputeHash(string input, Encoding encoding, Span<byte> buffer)
    {
        System.Diagnostics.Debug.Assert(buffer.Length >= Sha256HashSizeBytes, "buffer.Length must be at least 32");

        var maxInputSize = encoding.GetMaxByteCount(input.Length);
        var inputBuffer = maxInputSize <= 512
                              ? stackalloc byte[512]
                              : new byte[maxInputSize];
        var encodeCount = encoding.GetBytes(input, inputBuffer);
        var encodedInput = inputBuffer.Slice(0, encodeCount);

#if NET6_0_OR_GREATER
        var byteCount = SHA256.HashData(encodedInput, buffer);
#else
        using var sha256 = SHA256.Create();
        if (!sha256.TryComputeHash(encodedInput, buffer, out var byteCount))
        {
            ThrowHelper.ThrowArgumentException($"Error computing SHA256 hash for {input}: ");
        }
#endif

        System.Diagnostics.Debug.Assert(byteCount == Sha256HashSizeBytes, "we should always encode 32 bytes");
        return byteCount;
    }
#else
    public static byte[] ComputeHash(string input)
    {
        return ComputeHash(input, EncodingHelpers.Utf8NoBom);
    }

    private static byte[] ComputeHash(string input, Encoding encoding)
    {
        var maxInputSize = encoding.GetMaxByteCount(input.Length);
        byte[]? pooledArray = null;
        try
        {
            pooledArray = ArrayPool<byte>.Shared.Rent(maxInputSize);
            var encodeCount = encoding.GetBytes(input, charIndex: 0, charCount: input.Length, pooledArray, byteIndex: 0);

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(pooledArray, offset: 0, count: encodeCount);
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
