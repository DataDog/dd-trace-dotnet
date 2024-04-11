// <copyright file="NoThrowAwaiter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal struct NoThrowAwaiter : ICriticalNotifyCompletion
{
    private readonly Task _task;
    private readonly bool _preserveContext;

    public NoThrowAwaiter(Task task, bool preserveContext)
    {
        _task = task;
        _preserveContext = preserveContext;
    }

    public bool IsCompleted => _task.IsCompleted;

    public NoThrowAwaiter GetAwaiter() => this;

    public void GetResult()
    {
    }

    public void OnCompleted(Action continuation) => _task.ConfigureAwait(_preserveContext).GetAwaiter().OnCompleted(continuation);

    public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
}
