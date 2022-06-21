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

    private readonly object _listLock = new();
    private readonly List<KeyValuePair<string, string>> _tags;
    private string? _cachedPropagationHeader;

    public TraceTagCollection(List<KeyValuePair<string, string>>? tags = null, int maxHeaderLength = 512)
    public TraceTagCollection(List<KeyValuePair<string, string>>? tags = null, string? cachedPropagationHeader = null)
    {
        _tags = tags ?? new List<KeyValuePair<string, string>>(2);
        PropagationHeaderMaxLength = maxHeaderLength;
        _tags = tags;
        _cachedPropagationHeader = cachedPropagationHeader;
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="TraceTagCollection"/>.
    /// </summary>
    public int Count => _tags.Count;

    public int PropagationHeaderMaxLength { get; }

    public void SetTag(string name, string? value)
    {
        var isPropagated = name.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal);

        lock (_listLock)
        {
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
        if (_tags.Count > 0)
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
    public string ToPropagationHeader()
    {
        if (_cachedPropagationHeader == null)
        {
            lock (_listLock)
            {
                _cachedPropagationHeader = TagPropagation.ToHeader(this, PropagationHeaderMaxLength);
            }
        }

        return _cachedPropagationHeader;
    }

    public List<KeyValuePair<string, string>>.Enumerator GetEnumerator()
    {
        return _tags.GetEnumerator();
    }

    /// <summary>
    /// Returns the trace tags an <see cref="IEnumerable{T}"/>.
    /// Use for testing only as it will allocate on the heap.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> ToEnumerable()
    {
        return _tags;
    }
}
