// <copyright file="ICollectionWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

#pragma warning disable SA1400
#pragma warning disable SA1401

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Wrappers;

internal class ICollectionWrapper<T> : IEnumerableWrapper<T>, ICollection<T>
{
    private readonly Func<ICollection<T>?> _target;

    internal ICollectionWrapper(Func<ICollection<T>?>? target = null)
        : base(target)
    {
        _target = target ?? OnTargetCollection;
    }

    public int Count => _target()?.Count ?? 0;

    public bool IsReadOnly => _target()?.IsReadOnly ?? false;

    protected virtual ICollection<T>? OnTargetCollection()
    {
        return null;
    }

    protected override IEnumerable<T> OnTargetEnumerable()
    {
        return _target() ?? Array.Empty<T>();
    }

    public void Add(T item)
    {
        _target()?.Add(item);
    }

    public void Clear()
    {
        _target()?.Clear();
    }

    public bool Contains(T item)
    {
        return _target()?.Contains(item) ?? false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _target()?.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        return _target()?.Remove(item) ?? false;
    }
}
#endif
