// <copyright file="SpanPointers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Util;

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Buffers;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
#endif

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;

/// <summary>
///  SpanPointer helper methods
/// </summary>
internal static class SpanPointers
{
    // The pointer direction will always be down. The serverless agent handles cases where the
    // direction is up.
    private const string DownDirection = "d";
    private const string LinkKind = "span-pointer";
    private const int SpanPointerHashSizeBytes = 16;
    private const string S3PtrKind = "aws.s3.object";

    // S3 hashing rules: https://github.com/DataDog/dd-span-pointer-rules/blob/main/AWS/S3/Object/README.md
    public static void AddS3SpanPointer(Span span, string bucketName, string key, string eTag)
    {
        var hash = GeneratePointerHash(bucketName, key, eTag);

        var spanLinkAttributes = new List<KeyValuePair<string, string>>(4)
        {
            new("ptr.kind", S3PtrKind),
            new("ptr.dir", DownDirection),
            new("ptr.hash", hash),
            new("link.kind", LinkKind)
        };

        var spanLink = new SpanLink(SpanContext.ZeroContext, spanLinkAttributes);
        span.AddLink(spanLink);
    }

    // Hashing rules: https://github.com/DataDog/dd-span-pointer-rules/tree/main?tab=readme-ov-file#general-hashing-rules
    internal static string GeneratePointerHash(
        string bucketName,
        string key,
        string eTag)
    {
        // compute max buffer size for UTF-8 bytes
        // (faster than computing the actual byte count and good enough for the buffer size)
        var maxByteCount =
            Encoding.UTF8.GetMaxByteCount(bucketName.Length) +
            Encoding.UTF8.GetMaxByteCount(key.Length) +
            Encoding.UTF8.GetMaxByteCount(eTag.Length) + // if eTag is trimmed later, it won't make the max size larger so it's fine
            2; // '|' separator x 2

#if NETCOREAPP3_1_OR_GREATER
        // in .NET Core 3.1 and above, we can allocate the buffer
        // for the UTF-8 bytes on the stack if it's small enough
        if (maxByteCount < 256)
        {
            Span<byte> stackBuffer = stackalloc byte[maxByteCount];
            return ComputeHash(stackBuffer, bucketName, key, eTag);
        }
#endif

        // rent a buffer for the UTF-8 bytes
        var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: maxByteCount);

        try
        {
            return ComputeHash(buffer, bucketName, key, eTag);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    // .NET Core 3.1 and above have Encoding.UTF8.GetBytes() overload that writes to a Span<byte> buffer
    internal static string ComputeHash(
        Span<byte> buffer,
        string bucketName,
        string key,
        string eTag)
    {
        // trims double-quotes if both leading and trailing quotes are present
        // and there is at least one more character between them
        var trimmedETag = eTag is ['"', _, .., '"'] ? eTag.AsSpan(1, eTag.Length - 2) : eTag;
        var offset = 0;

        offset += Encoding.UTF8.GetBytes(bucketName, buffer[offset..]);
        buffer[offset++] = (byte)'|';
        offset += Encoding.UTF8.GetBytes(key, buffer[offset..]);
        buffer[offset++] = (byte)'|';
        offset += Encoding.UTF8.GetBytes(trimmedETag, buffer[offset..]);

        Span<byte> fullHash = stackalloc byte[32]; // SHA256 produces 32 bytes

#if NET6_0_OR_GREATER
        // .NET 6 has a static TryHashData() method that avoids the allocation of a SHA256 instance
        SHA256.TryHashData(buffer[..offset], fullHash, out _);
#else
        using var sha256 = SHA256.Create();
        sha256.TryComputeHash(buffer[..offset], fullHash, out _);
#endif

        var truncatedHash = fullHash[..SpanPointerHashSizeBytes];
        return HexString.ToHexString(truncatedHash);
    }
#else
    // .NET Framework and .NET Standard 2.0 do not have Encoding.UTF8.GetBytes() overload
    // that writes to a Span<byte>, so we fall back to a rented byte[]
    private static string ComputeHash(
        byte[] buffer,
        string bucketName,
        string key,
        string eTag)
    {
        var offset = 0;

        offset += Encoding.UTF8.GetBytes(bucketName, 0, bucketName.Length, buffer, offset);
        buffer[offset++] = (byte)'|';
        offset += Encoding.UTF8.GetBytes(key, 0, key.Length, buffer, offset);
        buffer[offset++] = (byte)'|';

        if (eTag.Length >= 2 && eTag[0] == '"' && eTag[eTag.Length - 1] == '"')
        {
            // trim double-quotes if both leading and trailing quotes are present
            // and there is at least one more character between them
            // (doing it here avoids allocating a new string with String.Substring())
            offset += Encoding.UTF8.GetBytes(eTag, 1, eTag.Length - 2, buffer, offset);
        }
        else
        {
            offset += Encoding.UTF8.GetBytes(eTag, 0, eTag.Length, buffer, offset);
        }

        using var sha256 = SHA256.Create();
        var fullHash = sha256.ComputeHash(buffer, offset: 0, count: offset);
        var truncatedHash = fullHash.AsSpan(0, SpanPointerHashSizeBytes);
        return HexString.ToHexString(truncatedHash);
    }
#endif
}
