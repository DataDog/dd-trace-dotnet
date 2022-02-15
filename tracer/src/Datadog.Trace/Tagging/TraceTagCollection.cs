// <copyright file="TraceTagCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal class TraceTagCollection
    {
        // key1=value1,key2=value2
        private const char TagPairSeparator = ',';
        private const char KeyValueSeparator = '=';

        // tags with this prefix are propagated horizontally
        // (i.e. from upstream services and to downstream services)
        // using the `x-datadog-tags` header
        private const string PropagatedTagPrefix = "_dd.p.";

        // for now we only expect one trace-level tag:
        // "_dd.p.upstream_services"
        private const int DefaultCapacity = 1;

        // "_dd.p.a=b"
        public const int MinimumPropagationHeaderLength = 9;
        public const int MaximumPropagationHeaderLength = 512;

        private static readonly char[] TagPairSeparators = { TagPairSeparator };

        private List<KeyValuePair<string, string>> _tags;
        private string? _cachedPropagationHeader;

        public TraceTagCollection()
        {
            _tags = new List<KeyValuePair<string, string>>(DefaultCapacity);
        }

        private TraceTagCollection(List<KeyValuePair<string, string>> tags)
        {
            _tags = tags;
        }

        public static TraceTagCollection ParseFromPropagationHeader(string? propagationHeader)
        {
            if (string.IsNullOrEmpty(propagationHeader))
            {
                return new TraceTagCollection();
            }

            var tags = propagationHeader!.Split(TagPairSeparators, StringSplitOptions.RemoveEmptyEntries);
            var tagList = new List<KeyValuePair<string, string>>(tags.Length);

            foreach (var tag in tags)
            {
                var separatorIndex = tag.IndexOf(KeyValueSeparator);

                // there must be at least one char before and
                // one char after the separator (e.g. "a=b")
                if (separatorIndex > 0 && separatorIndex < tag.Length - 1)
                {
                    var key = tag.Substring(0, separatorIndex);
                    var value = tag.Substring(separatorIndex + 1);
                    tagList.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return new TraceTagCollection(tagList)
                   {
                       // if the tags never change, we can reuse the same string we parsed
                       _cachedPropagationHeader = propagationHeader
                   };
        }

        public List<KeyValuePair<string, string>> AsList() => Volatile.Read(ref _tags);

        public void SetTag(string key, string? value)
        {
            var tags = AsList();

            lock (tags)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i].Key, key, StringComparison.Ordinal))
                    {
                        if (value == null)
                        {
                            tags.RemoveAt(i);
                        }
                        else
                        {
                            tags[i] = new KeyValuePair<string, string>(key, value);
                        }

                        // clear the cached value if we make any changes
                        _cachedPropagationHeader = null;
                        return;
                    }
                }

                // If we get there, the tag wasn't in the collection
                if (value != null)
                {
                    tags.Add(new KeyValuePair<string, string>(key, value));

                    // clear the cached value if we make any changes
                    _cachedPropagationHeader = null;
                }
            }
        }

        public string? GetTag(string key)
        {
            var tags = AsList();

            lock (tags)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (string.Equals(tags[i].Key, key, StringComparison.Ordinal))
                    {
                        return tags[i].Value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a collection of propagated internal Datadog tags,
        /// formatted as "key1=value1,key2=value2".
        /// </summary>
        public string ToPropagationHeader()
        {
            if (_cachedPropagationHeader == null)
            {
                // cache the propagated tags in a format ready
                // for headers in case we need it multiple times
                Interlocked.CompareExchange(ref _cachedPropagationHeader, FormatPropagationHeader(), null);
            }

            return _cachedPropagationHeader;
        }

        private string FormatPropagationHeader()
        {
            if (_tags.Count == 0)
            {
                return string.Empty;
            }

            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            foreach (var tag in _tags)
            {
                if (tag.Key.StartsWith(PropagatedTagPrefix) && !string.IsNullOrEmpty(tag.Value))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(TagPairSeparator);
                    }

                    sb.Append(tag.Key)
                      .Append(KeyValueSeparator)
                      .Append(tag.Value);
                }

                if (sb.Length > MaximumPropagationHeaderLength)
                {
                    // if combined tags are too long for propagation headers,
                    // don't set the header and instead set special "_dd.propagation_error:max_size" span tag
                    SetTag(TraceTagNames.Propagation.PropagationHeadersError, "max_size");
                    _ = StringBuilderCache.GetStringAndRelease(sb);
                    _cachedPropagationHeader = string.Empty;
                    return string.Empty;
                }
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
