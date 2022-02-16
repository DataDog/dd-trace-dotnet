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
        // "x-datadog-tags" header format is "key1=value1,key2=value2"
        private const char TagPairSeparator = ',';
        private const char KeyValueSeparator = '=';

        // tags with this prefix are propagated horizontally
        // (i.e. from upstream services and to downstream services)
        // using the "x-datadog-tags" header
        private const string PropagatedTagPrefix = "_dd.p.";
        private const int PropagatedTagPrefixLength = 6; // "_dd.p.".Length

        // for now we only expect one trace-level tag:
        // "_dd.p.upstream_services"
        private const int DefaultCapacity = 1;

        // ("_dd.p.".Length) + ("a=b".Length)
        public const int MinimumPropagationHeaderLength = PropagatedTagPrefixLength + 3;
        public const int MaximumPropagationHeaderLength = 512;

        private static readonly char[] TagPairSeparators = { TagPairSeparator };

        private readonly List<KeyValuePair<string, string>> _tags;
        private string? _cachedPropagationHeader;

        public TraceTagCollection()
        {
            _tags = new List<KeyValuePair<string, string>>(DefaultCapacity);
        }

        private TraceTagCollection(List<KeyValuePair<string, string>> tags)
        {
            _tags = tags;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="TraceTagCollection"/>.
        /// </summary>
        public int Count => _tags.Count;

        /// <summary>
        /// Parses the "x-datadog-tags" header value in "key1=value1,key2=value2" format.
        /// Propagated tags require the an "_dd.p.*" prefix, so any other tags are ignored.
        /// </summary>
        /// <param name="propagationHeader">The header value to parse.</param>
        /// <returns>A <see cref="TraceTagCollection"/> that contains the valid tags parsed from the specified header value.</returns>
        public static TraceTagCollection ParseFromPropagationHeader(string? propagationHeader)
        {
            if (string.IsNullOrEmpty(propagationHeader))
            {
                return new TraceTagCollection();
            }

            var tags = propagationHeader!.Split(TagPairSeparators, StringSplitOptions.RemoveEmptyEntries);
            var tagList = new List<KeyValuePair<string, string>>(tags.Length);
            var cacheOriginalHeader = true;

            foreach (var tag in tags)
            {
                // the shortest tag has the "_dd.p." prefix, a 1-character key, and 1-character value (e.g. "_dd.p.a=b")
                if (tag.Length >= MinimumPropagationHeaderLength &&
                    tag.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
                {
                    // NOTE: the first equals sign is the separator between key/value, but the tag value can contain
                    // additional equals signs, so make sure we only split on the _first_ one. For example,
                    // the "_dd.p.upstream_services" tag will have base64-encoded strings which use '=' for padding.
                    var separatorIndex = tag.IndexOf(KeyValueSeparator);

                    // "_dd.p.a=b"
                    //         ⬆   separator must be at index 7 or higher and before the end of string
                    //  012345678
                    if (separatorIndex > PropagatedTagPrefixLength &&
                        separatorIndex < tag.Length - 1)
                    {
                        var key = tag.Substring(0, separatorIndex);
                        var value = tag.Substring(separatorIndex + 1);
                        tagList.Add(new KeyValuePair<string, string>(key, value));
                    }
                    else
                    {
                        // skip invalid tag
                        cacheOriginalHeader = false;
                    }
                }
                else
                {
                    // skip invalid tag
                    cacheOriginalHeader = false;
                }
            }

            var traceTags = new TraceTagCollection(tagList);

            if (cacheOriginalHeader)
            {
                // we didn't skip any invalid tag, we can cache the original header string
                traceTags._cachedPropagationHeader = propagationHeader;
            }

            return traceTags;
        }

        public void SetTag(string key, string? value)
        {
            if (key is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(key));
            }

            lock (_tags)
            {
                bool tagsModified = false;

                for (int i = 0; i < _tags.Count; i++)
                {
                    if (string.Equals(_tags[i].Key, key, StringComparison.Ordinal))
                    {
                        if (value == null)
                        {
                            _tags.RemoveAt(i);
                            tagsModified = true;
                        }
                        else if (!string.Equals(_tags[i].Value, value, StringComparison.Ordinal))
                        {
                            _tags[i] = new KeyValuePair<string, string>(key, value);
                            tagsModified = true;
                        }

                        // clear the cached header if we make any changes to a distributed tag
                        if (tagsModified && key.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
                        {
                            _cachedPropagationHeader = null;
                        }

                        return;
                    }
                }

                // If we get there, the tag wasn't in the collection
                if (value != null)
                {
                    _tags.Add(new KeyValuePair<string, string>(key, value));

                    // clear the cached header if we make any changes to a distributed tag
                    if (key.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
                    {
                        _cachedPropagationHeader = null;
                    }
                }
            }
        }

        public string? GetTag(string key)
        {
            lock (_tags)
            {
                for (int i = 0; i < _tags.Count; i++)
                {
                    if (string.Equals(_tags[i].Key, key, StringComparison.Ordinal))
                    {
                        return _tags[i].Value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Constructs a string that can be used for horizontal propagation using the "x-datadog-tags" header
        /// in a "key1=value1,key2=value2" format. This header should only include tags with the "_dd.p.*" prefix.
        /// The returned string is cached and reused if no relevant tags are changed between calls.
        /// </summary>
        /// <returns>A string that can be used for horizontal propagation using the "x-datadog-tags" header.</returns>
        /// <seealso cref="FormatPropagationHeader"/>
        public string ToPropagationHeader()
        {
            if (_cachedPropagationHeader == null)
            {
                // cache the header in case we need it multiple times
                Interlocked.CompareExchange(ref _cachedPropagationHeader, FormatPropagationHeader(), null);
            }

            return _cachedPropagationHeader;
        }

        /// <seealso cref="ToPropagationHeader"/>
        private string FormatPropagationHeader()
        {
            if (_tags.Count == 0)
            {
                return string.Empty;
            }

            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            lock (_tags)
            {
                foreach (var tag in _tags)
                {
                    if (!string.IsNullOrEmpty(tag.Key) &&
                        !string.IsNullOrEmpty(tag.Value) &&
                        tag.Key.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
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
                        // if combined tags got too long for propagation headers,
                        // set tag "_dd.propagation_error:max_size"...
                        SetTag(TraceTagNames.Propagation.PropagationHeadersError, "max_size");

                        // ... and don't set the header
                        _cachedPropagationHeader = string.Empty;
                        return string.Empty;
                    }
                }
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        public List<KeyValuePair<string, string>>.Enumerator GetEnumerator()
        {
            return _tags.GetEnumerator();
        }
    }
}
