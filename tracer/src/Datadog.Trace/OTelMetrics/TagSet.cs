// <copyright file="TagSet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.OTelMetrics;

/// <summary>
/// Immutable key type for a set of attributes (tags).
/// Used to identify unique MetricPoint streams.
/// </summary>
internal readonly struct TagSet : IEquatable<TagSet>
{
    private static readonly IComparer<KeyValuePair<string, object?>> KeyComparer =
        Comparer<KeyValuePair<string, object?>>.Create(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

    private readonly string _key;

    private TagSet(string key) => _key = key;

    public static TagSet FromSpan(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var len = tags.Length;
        if (len == 0)
        {
            return new TagSet(string.Empty);
        }

        if (len == 1)
        {
            ref readonly var kv = ref tags[0];
            var sb = StringBuilderCache.Acquire();
            sb.Append(kv.Key).Append('=');
            if (kv.Value is not null)
            {
                sb.Append(kv.Value);
            }

            return new TagSet(StringBuilderCache.GetStringAndRelease(sb));
        }

        var sorted = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(len);
        try
        {
            tags.CopyTo(sorted);
            Array.Sort(sorted, 0, len, KeyComparer);

            var sb = StringBuilderCache.Acquire();
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                {
                    sb.Append(';');
                }

                ref readonly var kv = ref sorted[i];
                sb.Append(kv.Key).Append('=');
                if (kv.Value is not null)
                {
                    sb.Append(kv.Value);
                }
            }

            var key = StringBuilderCache.GetStringAndRelease(sb);
            return new TagSet(key);
        }
        finally
        {
            ArrayPool<KeyValuePair<string, object?>>.Shared.Return(sorted);
        }
    }

    public bool Equals(TagSet other) => _key == other._key;

    public override bool Equals(object? obj) => obj is TagSet other && Equals(other);

    public override int GetHashCode() => _key.GetHashCode();

    public override string ToString() => _key;
}
#endif
