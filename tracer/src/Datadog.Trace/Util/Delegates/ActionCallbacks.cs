// <copyright file="ActionCallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.Util.Delegates;

#pragma warning disable SA1402

internal abstract class ActionCallbacks
{
    public delegate void DelegateEnd(object? target, Exception? exception, object? state);

    public delegate void DelegateException(object? target, Exception exception);

    public DelegateEnd? OnDelegateEnd { get; set; }

    public DelegateException? OnException { get; set; }
}

internal class Action0Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target);

    public DelegateBegin? OnDelegateBegin { get; set; }
}

internal class Action1Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target, object? arg1);

    public DelegateBegin? OnDelegateBegin { get; set; }
}

internal class Action2Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target, object? arg1, object? arg2);

    public DelegateBegin? OnDelegateBegin { get; set; }
}

internal class Action3Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target, object? arg1, object? arg2, object? arg3);

    public DelegateBegin? OnDelegateBegin { get; set; }
}

internal class Action4Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target, object? arg1, object? arg2, object? arg3, object? arg4);

    public DelegateBegin? OnDelegateBegin { get; set; }
}

internal class Action5Callbacks : ActionCallbacks
{
    public delegate object? DelegateBegin(object? target, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5);

    public DelegateBegin? OnDelegateBegin { get; set; }
}
