// <copyright file="CoverageModuleValueStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Ci.Coverage;

#pragma warning disable DDSEAL001 // Tests derive from this allocation strategy to inject deterministic failures.
internal class CoverageModuleValueStrategy
#pragma warning restore DDSEAL001
{
    internal static readonly CoverageModuleValueStrategy Production = new(CoverageNativeAllocationDiagnostics.Process);

    internal CoverageModuleValueStrategy(CoverageNativeAllocationDiagnostics diagnostics)
    {
        Diagnostics = diagnostics;
    }

    internal CoverageNativeAllocationDiagnostics Diagnostics { get; }

    internal virtual IntPtr Allocate(int byteLength, CoverageModuleValueOrigin origin) => Marshal.AllocHGlobal(byteLength);

    internal virtual unsafe void Initialize(IntPtr pointer, int byteLength, CoverageModuleValueOrigin origin)
        => Unsafe.InitBlockUnaligned((byte*)pointer, 0, (uint)byteLength);

    internal virtual void Free(IntPtr pointer, CoverageModuleValueOrigin origin) => Marshal.FreeHGlobal(pointer);

    internal virtual void BeforeCapacityGrowth(CoverageModuleValueOrigin origin)
    {
    }

    internal virtual void BeforePublication(CoverageModuleValueOrigin origin)
    {
    }

    internal virtual void AfterPublication(CoverageModuleValueOrigin origin)
    {
    }
}
