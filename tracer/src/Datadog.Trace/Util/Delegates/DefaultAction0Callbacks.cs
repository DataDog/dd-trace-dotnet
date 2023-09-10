// <copyright file="DefaultAction0Callbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates;

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

    object? IAction0Callbacks.OnDelegateBegin(object? sender)
        => OnDelegateBegin?.Invoke(sender);

    void IActionCallbacks.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => OnDelegateEnd?.Invoke(sender, exception, state);

    void IActionCallbacks.OnException(object? sender, Exception ex)
        => OnException?.Invoke(sender, ex);
}
