// <copyright file="IMethodMalloc.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface IMethodMalloc : IUnknown
{
    public static readonly new Guid Guid = new("A0EFB28B-6EE2-4d7b-B983-A75EF7BEEDB8");

    IntPtr Alloc(uint cb)
    {
        NativeStubDiagnostics.NotImplemented("IMethodMalloc.Alloc");
        return default;
    }
}
#endif
