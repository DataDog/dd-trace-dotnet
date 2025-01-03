// <copyright file="ValueTaskHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal static class ValueTaskHelper
{
#if NETCOREAPP3_1_OR_GREATER
    public static bool IsValueTask(Type? returnValue)
        => returnValue == typeof(System.Threading.Tasks.ValueTask);

    public static bool IsGenericValueTask(Type? returnValue)
        => returnValue?.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.ValueTask<>);
#else
    // ValueTask isn't part of the BCL for .NET FX or .NET Standard, and is instead
    // provided by the System.Threading.Tasks.Extensions package. In .NET Core 2.1, ValueTask is available
    // the core lib, even though we can't directly reference it, so we don't need to worry about assembly
    // qualified references. In .NET FX (and .NET Standard generally) the type comes from a package.
    // In that case we can't rely on Type.GetType() because, we need to provide the fully qualified assembly names,
    // butt we also need support multiple versions of the package. So we fallback on the somewhat simple name check.
    private static readonly Type? ValueTaskType = Type.GetType("System.Threading.Tasks.ValueTask");
    private static readonly Type? GenericValueTaskType = Type.GetType("System.Threading.Tasks.ValueTask`1");

    public static bool IsValueTask(Type? returnValue)
        => ValueTaskType is not null
               ? returnValue == ValueTaskType
               : returnValue?.FullName == "System.Threading.Tasks.ValueTask";

    public static bool IsGenericValueTask(Type? returnValue)
    {
        var genericDefinition = returnValue?.GetGenericTypeDefinition();
        return GenericValueTaskType is not null
                   ? genericDefinition == GenericValueTaskType
                   : genericDefinition?.FullName == "System.Threading.Tasks.ValueTask`1";
    }
#endif

}
