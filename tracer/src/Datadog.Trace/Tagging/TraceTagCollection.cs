// <copyright file="TraceTagCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Tagging;

internal class TraceTagCollection
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceTagCollection>();

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

    public void SetTag(string name, string? value)
    {
        var isPropagated = name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal);

        lock (_listLock)
        {
            _tags ??= new List<KeyValuePair<string, string>>(1);

            if (_tags.Count > 0)
            {
                for (int i = 0; i < _tags.Count; i++)
                {
                    if (string.Equals(_tags[i].Key, name, StringComparison.Ordinal))
                    {
                        if (value == null)
                        {
                            _tags.RemoveAt(i);
                        }
                        else
                        {
                            _tags[i] = new(name, value);
                        }

                        // clear the cached header if we make any changes to a distributed tag
                        if (isPropagated)
                        {
                            _cachedPropagationHeader = null;
                        }

                        return;
                    }
                }
            }

            // tag not found, add new one
            if (value != null)
            {
                _tags.Add(new(name, value));

                // clear the cached header if we make any changes to a distributed tag
                if (isPropagated)
                {
                    _cachedPropagationHeader = null;
                }
            }
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
                    if (string.Equals(_tags[i].Key, name, StringComparison.Ordinal))
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
        if (_cachedPropagationHeader == null)
        {
            lock (_listLock)
            {
                _cachedPropagationHeader = TagPropagation.ToHeader(this, maxLength);
            }
        }

        return _cachedPropagationHeader;
    }

    public List<KeyValuePair<string, string>>.Enumerator GetEnumerator()
    {
        return _tags?.GetEnumerator() ?? EmptyEnumerator;
    }

    /// <summary>
    /// Returns the trace tags an <see cref="IEnumerable{T}"/>.
    /// Use for testing only as it will allocate the enumerator on the heap.
    /// </summary>
    internal IEnumerable<KeyValuePair<string, string>> ToEnumerable()
    {
        return _tags ?? (IEnumerable<KeyValuePair<string, string>>)Array.Empty<KeyValuePair<string, string>>();
    }
}
