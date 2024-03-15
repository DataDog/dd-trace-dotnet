// <copyright file="ICollectionWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// #nullable enable

#if !NETFRAMEWORK

#pragma warning disable SA1400
#pragma warning disable SA1401

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Wrappers;

internal class ICollectionWrapper<T> : IEnumerableWrapper<T>, ICollection<T>
{
    private readonly Func<ICollection<T>> _target;

    internal ICollectionWrapper(Func<ICollection<T>> target = null, Action<T> taint = null)
        : base(target, taint)
    {
        _target = target ?? OnTargetCollection;
    }

    public int Count => _target().Count;

    public bool IsReadOnly => _target().IsReadOnly;

    protected virtual ICollection<T> OnTargetCollection()
    {
        return null;
    }

    protected override IEnumerable<T> OnTargetEnumerable()
    {
        return _target();
    }

    public void Add(T item)
    {
        _target().Add(item);
    }

    public void Clear()
    {
        _target().Clear();
    }

    public bool Contains(T item)
    {
        return _target().Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        // Todo: target().SafeForEach(i => taint(i));
        _target().CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        return _target().Remove(item);
    }
}
#endif
