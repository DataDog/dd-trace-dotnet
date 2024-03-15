// <copyright file="IEnumeratorWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Wrappers;

#pragma warning disable SA1401
#pragma warning disable SA1402

internal class IEnumeratorWrapper : IEnumerator
{
    private readonly Func<IEnumerator> target;

    protected Action<object> taint;

    public IEnumeratorWrapper(Func<IEnumerator> target, Action<object> taint = null)
    {
        this.target = target;
        this.taint = taint ?? OnTaint;
    }

    public object Current
    {
        get
        {
            var res = target().Current;
            taint(res);
            return res;
        }
    }

    object IEnumerator.Current
    {
        get
        {
            return Current;
        }
    }

    protected virtual void OnTaint(object obj)
    {
    }

    public bool MoveNext()
    {
        return target().MoveNext();
    }

    public void Reset()
    {
        target().Reset();
    }
}

internal class IEnumeratorWrapper<T> : IEnumerator<T>
{
    private readonly IEnumerator<T> _target;

    protected Action<T> taint;

    internal IEnumeratorWrapper(IEnumerator<T> target, Action<T> taint = null)
    {
        _target = target;
        this.taint = taint ?? OnTaint;
    }

    public T Current
    {
        get
        {
            var res = _target.Current;
            taint(res);
            return res;
        }
    }

    object IEnumerator.Current
    {
        get
        {
            return Current;
        }
    }

    protected virtual void OnTaint(T obj)
    {
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
