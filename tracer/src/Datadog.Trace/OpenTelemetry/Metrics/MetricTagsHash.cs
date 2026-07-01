// <copyright file="MetricTagsHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.OpenTelemetry.Metrics;

/// <summary>
/// Computes an allocation-free 64-bit FNV hash of a set of metric attributes (tags), used as the
/// dictionary key that identifies a unique <see cref="MetricPoint"/> stream. Tags are sorted by
/// key (ordinal) before hashing so that the same attribute set produces the same
/// hash regardless of the order the attributes were supplied in.
/// </summary>
internal static class MetricTagsHash
{
    private const FnvHash64.Version HashVersion = FnvHash64.Version.V1A;

    private static readonly IComparer<KeyValuePair<string, object?>> KeyComparer =
        Comparer<KeyValuePair<string, object?>>.Create(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

    public static ulong Compute(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var len = tags.Length;
        if (len == 0)
        {
            return FnvHash64.Empty;
        }

        var hash = FnvHash64.Empty;

        // No need to allocate to sort if only a single item
        if (len == 1)
        {
            return HashPair(tags[0], hash);
        }

        var sorted = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(len);
        try
        {
            tags.CopyTo(sorted);
            Array.Sort(sorted, 0, len, KeyComparer);

            for (var i = 0; i < len; i++)
            {
                hash = HashPair(sorted[i], hash);
            }

            return hash;
        }
        finally
        {
            ArrayPool<KeyValuePair<string, object?>>.Shared.Return(sorted);
        }
    }

    [System.Runtime.CompilerServices.SkipLocalsInit] // avoid init the stackalloc buffer
    private static ulong HashPair(KeyValuePair<string, object?> tag, ulong hash)
    {
        hash = HashChars(tag.Key, hash);

        if (tag.Value is null)
        {
            // Treat this as identical to empty string, so just record the 0 length
            return HashInt(0, hash);
        }

        // If we were targeting .NET 8, we could use IUtf8SpanFormattable to avoid the two steps but we aren't
        if (tag.Value is ISpanFormattable spanFormattable)
        {
            // Format directly into a stack buffer to avoid the intermediate string allocation.
            // Most tag values (numbers, bools, chars, enums) comfortably fit in this buffer.
            Span<char> buffer = stackalloc char[128];

            if (spanFormattable.TryFormat(buffer, out var written, format: default, provider: CultureInfo.InvariantCulture))
            {
                return HashChars(buffer.Slice(0, written), hash);
            }

            // Didn't fit; fall back to ToString()
        }

        var stringValue = tag.Value is IFormattable formattable
                              ? formattable.ToString(format: null, CultureInfo.InvariantCulture)
                              : tag.Value.ToString();

        return string.IsNullOrEmpty(stringValue)
                   ? HashInt(0, hash)
                   : HashChars(stringValue, hash);
    }

    // The hash is only an in-process dictionary key for a MetricPoint stream; it is never persisted or
    // compared across processes. So rather than transcoding to UTF-8 (as FnvHash64's string overloads
    // do), we hash the raw UTF-16 char bytes directly, which avoids the encoding step. We include the
    // length of the char bytes as a prefix to avoid collisions.
    private static ulong HashChars(ReadOnlySpan<char> chars, ulong hash)
    {
        hash = HashInt(chars.Length, hash);
        return FnvHash64.GenerateHash(MemoryMarshal.AsBytes(chars), HashVersion, hash);
    }

    // Hashes an int as fixed-width bytes. Used to length-prefix each field so the hashed byte stream
    // is deterministically decodable; endianness is irrelevant as the hash is only used in-process.
    private static ulong HashInt(int value, ulong hash)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, value);
        return FnvHash64.GenerateHash(bytes, HashVersion, hash);
    }
}
#endif
