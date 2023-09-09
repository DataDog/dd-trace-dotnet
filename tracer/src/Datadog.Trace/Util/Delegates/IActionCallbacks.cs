// <copyright file="IActionCallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates;

internal interface IActionCallbacks
{
    void OnDelegateEnd(object? sender, Exception? exception, object? state);

    void OnException(object? sender, Exception ex);
}

internal interface IAction0Callbacks : IActionCallbacks
{
    object? OnDelegateBegin(object? sender);
}

internal interface IAction1Callbacks : IActionCallbacks
{
    object? OnDelegateBegin<TArg1>(object? sender, ref TArg1 arg1);
}

internal interface IAction2Callbacks : IActionCallbacks
{
    object? OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 arg1, ref TArg2 arg2);
}

internal interface IAction3Callbacks : IActionCallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3);
}

internal interface IAction4Callbacks : IActionCallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4);
}

internal interface IAction5Callbacks : IActionCallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5);
}

internal readonly struct DefaultAction0Callbacks : IAction0Callbacks
{
    public readonly DelegateBegin? OnDelegateBegin;
    public readonly DelegateEnd? OnDelegateEnd;
    public readonly ExceptionDelegate? OnException;

    public DefaultAction0Callbacks(DelegateBegin? onDelegateBegin = null, DelegateEnd? onDelegateEnd = null, ExceptionDelegate? onException = null)
    {
        OnDelegateBegin = onDelegateBegin;
        OnDelegateEnd = onDelegateEnd;
        OnException = onException;
    }

    public delegate object? DelegateBegin(object? sender);

    public delegate void DelegateEnd(object? sender, Exception? exception, object? state);

    public delegate void ExceptionDelegate(object? sender, Exception ex);

    object? IAction0Callbacks.OnDelegateBegin(object? sender)
        => OnDelegateBegin?.Invoke(sender);

    void IActionCallbacks.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => OnDelegateEnd?.Invoke(sender, exception, state);

    void IActionCallbacks.OnException(object? sender, Exception ex)
        => OnException?.Invoke(sender, ex);
}
