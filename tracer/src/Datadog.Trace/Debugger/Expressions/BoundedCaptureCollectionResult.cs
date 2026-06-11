// <copyright file="BoundedCaptureCollectionResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Expressions;

internal sealed class BoundedCaptureCollectionResult<T> : IReadOnlyCollection<T>, IBoundedCaptureCollectionResult
{
    private readonly List<T> _items;

    internal BoundedCaptureCollectionResult(List<T> items, bool wasTruncated, bool isDictionary)
    {
        _items = items;
        WasTruncated = wasTruncated;
        IsDictionary = isDictionary;
    }

    public int Count => _items.Count;

    public bool WasTruncated { get; }

    public bool IsDictionary { get; }

    public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
