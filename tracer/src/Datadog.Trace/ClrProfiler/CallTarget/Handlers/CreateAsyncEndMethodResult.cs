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

    public CreateAsyncEndMethodResult(DynamicMethod method, bool preserveContext)
    {
        Method = method;
        PreserveContext = preserveContext;
    }
}
