// <copyright file="DefaultAction4Callbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates;

internal readonly struct DefaultAction4Callbacks : IAction4Callbacks
{
    public readonly DelegateBegin? OnDelegateBegin;
    public readonly DelegateEnd? OnDelegateEnd;
    public readonly ExceptionDelegate? OnException;

    public DefaultAction4Callbacks(DelegateBegin? onDelegateBegin = null, DelegateEnd? onDelegateEnd = null, ExceptionDelegate? onException = null)
    {
        OnDelegateBegin = onDelegateBegin;
        OnDelegateEnd = onDelegateEnd;
        OnException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3, object? arg4);

    object? IAction4Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4)
        => OnDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4);

    void IActionCallbacks.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => OnDelegateEnd?.Invoke(sender, exception, state);

    void IActionCallbacks.OnException(object? sender, Exception ex)
        => OnException?.Invoke(sender, ex);
}
