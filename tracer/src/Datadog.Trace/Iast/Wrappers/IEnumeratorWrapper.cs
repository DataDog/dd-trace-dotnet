// <copyright file="IEnumeratorWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Wrappers;

#pragma warning disable SA1401
#pragma warning disable SA1402

internal class IEnumeratorWrapper : IEnumerator
{
    private readonly Func<IEnumerator> _target;

    public IEnumeratorWrapper(Func<IEnumerator> target, Action<object> taint = null)
    {
        _target = target;
    }

    public object Current
    {
        get
        {
            return _target().Current;
        }
    }

    object IEnumerator.Current
    {
        get
        {
            return Current;
        }
    }

    public bool MoveNext()
    {
        return _target().MoveNext();
    }

    public void Reset()
    {
        _target().Reset();
    }
}

internal class IEnumeratorWrapper<T> : IEnumerator<T>
{
    private readonly IEnumerator<T> _target;

    internal IEnumeratorWrapper(IEnumerator<T> target, Action<T> taint = null)
    {
        _target = target;
    }

    public T Current
    {
        get
        {
            return _target.Current;
        }
    }

    object IEnumerator.Current
    {
        get
        {
            return Current;
        }
    }

    public void Dispose()
    {
        _target.Dispose();
    }

    public bool MoveNext()
    {
        return _target.MoveNext();
    }

    public void Reset()
    {
        _target.Reset();
    }
}
#endif
