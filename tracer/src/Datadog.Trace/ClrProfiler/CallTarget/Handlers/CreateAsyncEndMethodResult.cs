// <copyright file="CreateAsyncEndMethodResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Reflection.Emit;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal readonly struct CreateAsyncEndMethodResult
{
    public readonly DynamicMethod? Method;
    public readonly bool PreserveContext;

    /// <summary>
    /// Whether the integration's OnAsyncMethodEnd is itself async, so <see cref="Method"/> returns
    /// Task&lt;TReturn&gt; rather than TReturn. Recorded here because it cannot be recovered from
    /// <see cref="Method"/>: when TReturn is itself task-like - the unwrapped T of a
    /// <c>Task&lt;Task&gt;</c>-returning runtime-async target - a synchronous callback's dynamic
    /// method also returns a Task, and the two are indistinguishable by return type alone.
    /// </summary>
    public readonly bool IsTaskReturn;

    public CreateAsyncEndMethodResult(DynamicMethod method, bool preserveContext, bool isTaskReturn)
    {
        Method = method;
        PreserveContext = preserveContext;
        IsTaskReturn = isTaskReturn;
    }
}
