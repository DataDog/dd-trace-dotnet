// <copyright file="TraceTagCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tagging
{
    internal class TraceTagCollection
    {
        // used when tag list is null because "new List<KeyValuePair<string, string>>.Enumerator" returns an invalid enumerator.
        private static readonly List<KeyValuePair<string, string>>.Enumerator EmptyEnumerator = new List<KeyValuePair<string, string>>(0).GetEnumerator();

        private readonly object _listLock = new();

        private List<KeyValuePair<string, string>>? _tags;

        private string? _cachedPropagationHeader;

        public TraceTagCollection(List<KeyValuePair<string, string>>? tags = null, string? cachedPropagationHeader = null)
        {
            _tags = tags;
            _cachedPropagationHeader = cachedPropagationHeader;
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="TraceTagCollection"/>.
        /// </summary>
        public int Count => _tags?.Count ?? 0;

        public bool SetTag(string name, string? value, bool replaceIfExists = true)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var isPropagated = name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.OrdinalIgnoreCase);

            lock (_listLock)
            {
                if (_tags?.Count > 0)
                {
                    // we have some tags already, try to find this one
                    for (int i = 0; i < _tags.Count; i++)
                    {
                        if (string.Equals(_tags[i].Key, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (replaceIfExists)
                            {
                                if (value == null)
                                {
                                    // tag already exists, setting it to null removes it
                                    _tags.RemoveAt(i);
                                }
                                else if (!string.Equals(_tags[i].Value, value, StringComparison.Ordinal))
                                {
                                    // tag already exists with different value, replace it
                                    _tags[i] = new(name, value);

                                    // clear the cached header
                                    if (isPropagated)
                                    {
                                        _cachedPropagationHeader = null;
                                    }
                                }

                                return true;
                            }

                            // tag exists and replaceIfExists is false
                            return false;
                        }
                    }
                }

                // tag not found, add new one
                if (value != null)
                {
                    // delay creating the List<T> as long as possible
                    _tags ??= new List<KeyValuePair<string, string>>(1);

                    _tags.Add(new(name, value));

                    // clear the cached header
                    if (isPropagated)
                    {
                        _cachedPropagationHeader = null;
                    }

                    return true;
                }

                // new value is null and tag did not exist, nothing to insert or remove
                return false;
            }
        }

        public string? GetTag(string name)
        {
            if (_tags?.Count > 0)
            {
                lock (_listLock)
                {
                    for (int i = 0; i < _tags.Count; i++)
                    {
                        if (string.Equals(_tags[i].Key, name, StringComparison.OrdinalIgnoreCase))
                        {
                            return _tags[i].Value;
                        }
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
        public string ToPropagationHeader(int maxLength)
        {
            return _cachedPropagationHeader ??= TagPropagation.ToHeader(this, maxLength);
        }

        /// <summary>
        /// Returns the trace tags an <see cref="IEnumerable{T}"/>.
        /// Use for testing only as it will allocate the enumerator on the heap.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, string>> ToEnumerable()
        {
            return _tags ?? (IEnumerable<KeyValuePair<string, string>>)Array.Empty<KeyValuePair<string, string>>();
        }

        public KeyValuePair<string, string>[] ToArray()
        {
            if (_tags == null || _tags.Count == 0)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            lock (_listLock)
            {
                return _tags.ToArray();
            }
        }
    }
}
