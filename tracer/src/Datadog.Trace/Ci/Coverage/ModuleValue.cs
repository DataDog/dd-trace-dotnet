// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.Ci.Coverage.Metadata;
using Unsafe = Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe;

namespace Datadog.Trace.Ci.Coverage;

internal class ModuleValue : IDisposable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ModuleValue(ModuleCoverageMetadata metadata, Module module, int fileLinesMemorySize)
    {
        Metadata = metadata;
        Module = module;
        FilesLines = Marshal.AllocHGlobal(fileLinesMemorySize);
        Unsafe.InitBlockUnaligned((byte*)FilesLines, 0, (uint)fileLinesMemorySize);
    }

    ~ModuleValue()
    {
        Dispose();
    }

    public ModuleCoverageMetadata Metadata { get; }

    public Module Module { get; }

    public IntPtr FilesLines { get; private set; }

    public void Dispose()
    {
        var filesLines = FilesLines;
        if (filesLines != IntPtr.Zero)
        {
            FilesLines = IntPtr.Zero;
            Marshal.FreeHGlobal(filesLines);
        }

        GC.SuppressFinalize(this);
    }
}
