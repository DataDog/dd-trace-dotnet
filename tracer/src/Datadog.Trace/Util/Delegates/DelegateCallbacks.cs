// <copyright file="DelegateCallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1649

internal delegate void DelegateEnd(object? sender, Exception? exception, object? state);

internal delegate object? DelegateReturnEnd(object? sender, object? returnValue, Exception? exception, object? state);

internal delegate Task<object?> DelegateReturnAsyncEnd(object? sender, object? returnValue, Exception? exception, object? state);

internal delegate void ExceptionDelegate(object? sender, Exception ex);

internal readonly struct DelegateAction0Callbacks : IBegin0Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction0Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender);

    object? IBegin0Callbacks.OnDelegateBegin(object? sender)
        => _onDelegateBegin?.Invoke(sender);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateAction1Callbacks : IBegin1Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction1Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1);

    object? IBegin1Callbacks.OnDelegateBegin<TArg1>(object? sender, ref TArg1 arg1)
        => _onDelegateBegin?.Invoke(sender, arg1);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateAction2Callbacks : IBegin2Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction2Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2);

    object? IBegin2Callbacks.OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 arg1, ref TArg2 arg2)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateAction3Callbacks : IBegin3Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction3Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3);

    object? IBegin3Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateAction4Callbacks : IBegin4Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction4Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3, object? arg4);

    object? IBegin4Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateAction5Callbacks : IBegin5Callbacks, IVoidReturnCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateEnd? _onDelegateEnd;
    private readonly ExceptionDelegate? _onException;

    public DelegateAction5Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onException = onException;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5);

    object? IBegin5Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4, arg5);

    void IVoidReturnCallback.OnDelegateEnd(object? sender, Exception? exception, object? state)
        => _onDelegateEnd?.Invoke(sender, exception, state);

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);
}

internal readonly struct DelegateFunc0Callbacks : IBegin0Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc0Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin0Callbacks.OnDelegateBegin(object? sender)
        => _onDelegateBegin?.Invoke(sender);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}

internal readonly struct DelegateFunc1Callbacks : IBegin1Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc1Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin1Callbacks.OnDelegateBegin<TArg1>(object? sender, ref TArg1 arg1)
        => _onDelegateBegin?.Invoke(sender, arg1);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}

internal readonly struct DelegateFunc2Callbacks : IBegin2Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc2Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin2Callbacks.OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 arg1, ref TArg2 arg2)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}

internal readonly struct DelegateFunc3Callbacks : IBegin3Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc3Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin3Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}

internal readonly struct DelegateFunc4Callbacks : IBegin4Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc4Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3, object? arg4);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin4Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}

internal readonly struct DelegateFunc5Callbacks : IBegin5Callbacks, IReturnCallback, IReturnAsyncCallback
{
    private readonly DelegateBegin? _onDelegateBegin;
    private readonly DelegateReturnEnd? _onDelegateEnd;
    private readonly DelegateReturnAsyncEnd? _onDelegateAsyncEnd;
    private readonly ExceptionDelegate? _onException;
    private readonly bool _preserveAsyncContext;

    public DelegateFunc5Callbacks(
        DelegateBegin? onDelegateBegin = null,
        DelegateReturnEnd? onDelegateEnd = null,
        ExceptionDelegate? onException = null,
        DelegateReturnAsyncEnd? onDelegateAsyncEnd = null,
        bool preserveAsyncContext = false)
    {
        _onDelegateBegin = onDelegateBegin;
        _onDelegateEnd = onDelegateEnd;
        _onDelegateAsyncEnd = onDelegateAsyncEnd;
        _onException = onException;
        _preserveAsyncContext = preserveAsyncContext;
    }

    public delegate object? DelegateBegin(object? sender, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5);

    bool IReturnAsyncCallback.PreserveAsyncContext => _preserveAsyncContext;

    object? IBegin5Callbacks.OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5)
        => _onDelegateBegin?.Invoke(sender, arg1, arg2, arg3, arg4, arg5);

    TReturn IReturnCallback.OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state)
        => (TReturn)_onDelegateEnd?.Invoke(sender, returnValue, exception, state)!;

    void ICallbacks.OnException(object? sender, Exception ex)
        => _onException?.Invoke(sender, ex);

    async Task<TInnerReturn> IReturnAsyncCallback.OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state)
    {
        if (_onDelegateAsyncEnd is not null)
        {
            return ((TInnerReturn?)await _onDelegateAsyncEnd.Invoke(sender, returnValue, exception, state).ConfigureAwait(false))!;
        }

        return default!;
    }
}
