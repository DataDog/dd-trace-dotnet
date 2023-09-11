// <copyright file="ICallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Delegates;

internal interface ICallbacks
{
    void OnException(object? sender, Exception ex);
}

internal interface IVoidReturnCallback : ICallbacks
{
    void OnDelegateEnd(object? sender, Exception? exception, object? state);
}

internal interface IReturnCallback : ICallbacks
{
    TReturn OnDelegateEnd<TReturn>(object? sender, TReturn returnValue, Exception? exception, object? state);
}

internal interface IReturnAsyncCallback : ICallbacks
{
    bool PreserveAsyncContext { get; }

    Task<TInnerReturn> OnDelegateEndAsync<TInnerReturn>(object? sender, TInnerReturn returnValue, Exception? exception, object? state);
}

internal interface IBegin0Callbacks : ICallbacks
{
    object? OnDelegateBegin(object? sender);
}

internal interface IBegin1Callbacks : ICallbacks
{
    object? OnDelegateBegin<TArg1>(object? sender, ref TArg1 arg1);
}

internal interface IBegin2Callbacks : ICallbacks
{
    object? OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 arg1, ref TArg2 arg2);
}

internal interface IBegin3Callbacks : ICallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3);
}

internal interface IBegin4Callbacks : ICallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3, TArg4>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4);
}

internal interface IBegin5Callbacks : ICallbacks
{
    object? OnDelegateBegin<TArg1, TArg2, TArg3, TArg4, TArg5>(object? sender, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3, ref TArg4 arg4, ref TArg5 arg5);
}
