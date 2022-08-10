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

        /// <summary>
        /// Adds a new tag to the collection.
        /// If the tag already exists, is not modified.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <param name="value">The value of the tag.</param>
        /// <returns><c>true</c> if the tag is added to the collection, <c>false</c> otherwise.</returns>
        public bool TryAddTag(string name, string value)
        {
            if (value == null!)
            {
                return false;
            }

            return SetTag(name, value, replaceIfExists: false);
        }

        /// <summary>
        /// Adds a new tag to the collection if it doesn't already exists,
        /// or updates the tag with a new value if it already exists.
        /// If the tag value is <c>null</c>, the tag is not added to the collection,
        /// and its previous value is removed if found.
        /// </summary>
        /// <param name="name">The name of the tag.</param>
        /// <param name="value">The value of the tag.</param>
        /// <returns>
        /// <c>true</c> if the collection is modified by adding, updating, or removing a tag,
        /// <c>false</c> otherwise.
        /// </returns>
        public bool SetTag(string name, string? value)
        {
            return SetTag(name, value, replaceIfExists: true);
        }

        private bool SetTag(string name, string? value, bool replaceIfExists)
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
                            // found the tag
                            if (replaceIfExists)
                            {
                                if (value == null)
                                {
                                    // tag already exists, remove it
                                    _tags.RemoveAt(i);

                                    // clear the cached header
                                    if (isPropagated)
                                    {
                                        _cachedPropagationHeader = null;
                                    }

                                    return true;
                                }

                                if (!string.Equals(_tags[i].Value, value, StringComparison.Ordinal))
                                {
                                    // tag already exists with different value, replace it
                                    _tags[i] = new(name, value);

                                    // clear the cached header
                                    if (isPropagated)
                                    {
                                        _cachedPropagationHeader = null;
                                    }

                                    return true;
                                }
                            }

                            // tag exists but replaceIfExists is false, don't modify anything
                            return false;
                        }
                    }
                }

                // tag not found
                if (value != null)
                {
                    // delay creating the List<T> as long as possible
                    _tags ??= new List<KeyValuePair<string, string>>(1);

                    _tags.Add(new(name, value));

                    // clear the cached header if we added a propagated tag
                    if (isPropagated)
                    {
                        _cachedPropagationHeader = null;
                    }

                    return true;
                }

                // tag not found and value == null so tag was not added
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

        public void SetTags(TraceTagCollection? tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return;
            }

            foreach (var tag in tags.ToArray())
            {
                SetTag(tag.Key, tag.Value);
            }
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
