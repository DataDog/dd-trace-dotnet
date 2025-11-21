// <copyright file="SpanPointers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb;
using Datadog.Trace.Util;

#if NETCOREAPP3_1_OR_GREATER
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
    private const string DynamoDbPtrKind = "aws.dynamodb.item";

    // S3 hashing rules: https://github.com/DataDog/dd-span-pointer-rules/blob/main/AWS/S3/Object/README.md
    public static void AddS3SpanPointer(Span span, string? bucketName, string? key, string? eTag)
    {
        if (bucketName is null || key is null || eTag == null)
        {
            return;
        }

        var components = ConcatenateComponents(bucketName, key, eTag);
        var hash = GeneratePointerHash(components);

        var spanLinkAttributes = new List<KeyValuePair<string, string>>(4)
        {
            new("ptr.kind", S3PtrKind),
            new("ptr.dir", DownDirection),
            new("ptr.hash", hash),
            new("link.kind", LinkKind)
        };

        var spanLink = new SpanLink(SpanContext.Zero, spanLinkAttributes);
        span.AddLink(spanLink);
    }

    // DynamoDB hashing rules: https://github.com/DataDog/dd-span-pointer-rules/blob/main/AWS/DynamoDB/Item/README.md
    public static void AddDynamoDbSpanPointer(Span span, string? tableName, IDynamoDbKeysObject? keys)
    {
        if (keys?.Instance is null || keys.KeyNames is null || !keys.KeyNames.Any() || tableName is null)
        {
            return;
        }

        var sortedKeys = keys.KeyNames.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var key1 = sortedKeys[0];
        var value1 = AwsDynamoDbCommon.GetValueFromDynamoDbAttribute(keys[key1]);
        var key2 = string.Empty;
        var value2 = string.Empty;

        if (sortedKeys.Length > 1)
        {
            key2 = sortedKeys[1];
            value2 = AwsDynamoDbCommon.GetValueFromDynamoDbAttribute(keys[key2]);
        }

        var components = ConcatenateComponents(tableName, key1, value1, key2, value2);
        var hash = GeneratePointerHash(components);

        var spanLinkAttributes = new List<KeyValuePair<string, string>>(4)
        {
            new("ptr.kind", DynamoDbPtrKind),
            new("ptr.dir", DownDirection),
            new("ptr.hash", hash),
            new("link.kind", LinkKind)
        };

        var spanLink = new SpanLink(SpanContext.Zero, spanLinkAttributes);
        span.AddLink(spanLink);
    }

    internal static string ConcatenateComponents(string bucketName, string key, string eTag)
    {
        var builder = StringBuilderCache.Acquire();
        builder.Append(bucketName);
        builder.Append('|');
        builder.Append(key);
        builder.Append('|');

        // ReSharper disable once MergeIntoPattern
        // ReSharper disable once UseIndexFromEndExpression
        if (eTag.Length >= 2 && eTag[0] == '"' && eTag[eTag.Length - 1] == '"')
        {
            // trim double-quotes around eTag if both leading and trailing quotes are present
            // and there is at least one more character between them
            // (avoid allocating a new string with String.Substring())
            builder.Append(eTag, 1, eTag.Length - 2);
        }
        else
        {
            builder.Append(eTag);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    internal static string ConcatenateComponents(string tableName, string key1, string value1, string key2, string value2)
    {
        return $"{tableName}|{key1}|{value1}|{key2}|{value2}";
    }

    // Hashing rules: https://github.com/DataDog/dd-span-pointer-rules/tree/main?tab=readme-ov-file#general-hashing-rules
    internal static string GeneratePointerHash(string components)
    {
        // compute max buffer size for UTF-8 bytes
        // (faster than computing the actual byte count and good enough for the buffer size)
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(components.Length);

#if NETCOREAPP3_1_OR_GREATER
        // in .NET Core 3.1 and above, we can allocate the buffer
        // for the UTF-8 bytes on the stack if it's small enough
        if (maxByteCount < 256)
        {
            Span<byte> stackBuffer = stackalloc byte[maxByteCount];
            return ComputeHash(components, stackBuffer);
        }
#endif
        // rent a buffer for the UTF-8 bytes
        var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: maxByteCount);

        try
        {
            return ComputeHash(components, buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    // .NET Core 3.1 and above have Encoding.UTF8.GetBytes() overload that writes to a Span<byte> buffer
    internal static string ComputeHash(string components, Span<byte> buffer)
    {
        var byteCount = Encoding.UTF8.GetBytes(components, buffer);
        Span<byte> fullHash = stackalloc byte[32]; // SHA256 produces 32 bytes

#if NET6_0_OR_GREATER
        // .NET 6 has a static TryHashData() method that avoids the allocation of a SHA256 instance
        SHA256.TryHashData(buffer[..byteCount], fullHash, out _);
#else
        using var sha256 = SHA256.Create();
        sha256.TryComputeHash(buffer[..byteCount], fullHash, out _);
#endif

        var truncatedHash = fullHash[..SpanPointerHashSizeBytes];
        return HexString.ToHexString(truncatedHash);
    }
#else
    // .NET Framework and .NET Standard 2.0 do not have Encoding.UTF8.GetBytes() overload
    // that writes to a Span<byte>, so we fall back to a rented byte[]
    internal static string ComputeHash(string components, byte[] buffer)
    {
        var byteCount = Encoding.UTF8.GetBytes(
            components,
            charIndex: 0,
            charCount: components.Length,
            bytes: buffer,
            byteIndex: 0);

        using var sha256 = SHA256.Create();
        var fullHash = sha256.ComputeHash(buffer, offset: 0, count: byteCount);
        var truncatedHash = fullHash.AsSpan(0, SpanPointerHashSizeBytes);
        return HexString.ToHexString(truncatedHash);
    }
#endif
}
