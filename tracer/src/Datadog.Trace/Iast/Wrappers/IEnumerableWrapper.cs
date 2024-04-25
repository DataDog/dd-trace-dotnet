// <copyright file="IEnumerableWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// #nullable enable

#if !NETFRAMEWORK

#pragma warning disable SA1400
#pragma warning disable SA1401

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.Iast.Wrappers;

internal class IEnumerableWrapper<T> : IEnumerable<T>
{
    readonly Func<IEnumerable<T>> target;

    protected Action<T> taint;

    internal IEnumerableWrapper(Func<IEnumerable<T>> target = null, Action<T> taint = null)
    {
        this.target = target ?? OnTargetEnumerable;
        this.taint = taint ?? OnTaint;
    }

    protected virtual IEnumerable<T> OnTargetEnumerable()
    {
        return null;
    }

    protected virtual void OnTaint(T obj)
    {
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new IEnumeratorWrapper<T>(target().GetEnumerator(), taint);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new IEnumeratorWrapper(() => ((IEnumerable)target()).GetEnumerator(), o => taint((T)o));
    }
}
#endif
