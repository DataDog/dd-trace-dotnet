// <copyright file="TagSet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// Immutable key type for a set of attributes (tags).
    /// Used to identify unique MetricPoint streams.
    /// </summary>
    internal readonly struct TagSet : IEquatable<TagSet>
    {
        private readonly string _key;

        private TagSet(string key) => _key = key;

        public static TagSet FromSpan(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (tags.Length == 0)
            {
                return new TagSet(string.Empty);
            }

            // Order keys deterministically to ensure stable equality
            var sorted = tags.ToArray();
            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Key, b.Key));

            var key = string.Join(";", sorted.Select(kv => $"{kv.Key}={kv.Value}"));
            return new TagSet(key);
        }

        public bool Equals(TagSet other) => _key == other._key;

        public override bool Equals(object? obj) => obj is TagSet other && Equals(other);

        public override int GetHashCode() => _key.GetHashCode();

        public override string ToString() => _key;
    }
}
#endif
