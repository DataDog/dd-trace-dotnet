// <copyright file="MethodMalloc.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

internal sealed class MethodMalloc : IMethodMalloc
{
    private readonly EmulatedRuntime _runtime;

    public MethodMalloc(EmulatedRuntime runtime)
    {
        _runtime = runtime;
    }

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef() => 1;

    public int Release() => 1;

    public IntPtr Alloc(uint cb) => _runtime.Allocate(cb);
}
#endif
