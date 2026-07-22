// <copyright file="ModuleValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class ModuleValue : IDisposable
{
    private readonly CoverageModuleValueStrategy _strategy;
    private readonly CoverageModuleValueOrigin _origin;
    private IntPtr _filesLines;
    private int _allocatedByteLength;

    public ModuleValue(ModuleCoverageMetadata metadata, Module module, int fileLinesMemorySize, CoverageModuleValueStrategy strategy, CoverageModuleValueOrigin origin)
    {
        Metadata = metadata;
        Module = module;
        _strategy = strategy;
        _origin = origin;

        var pointer = IntPtr.Zero;
        try
        {
            pointer = strategy.Allocate(fileLinesMemorySize, origin);
            strategy.Initialize(pointer, fileLinesMemorySize, origin);
            strategy.Diagnostics.OnAllocated(origin, fileLinesMemorySize);
            _allocatedByteLength = fileLinesMemorySize;
            _filesLines = pointer;
            pointer = IntPtr.Zero;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                strategy.Free(pointer, origin);
            }
        }
    }

    ~ModuleValue()
    {
        Dispose();
    }

    public ModuleCoverageMetadata Metadata { get; }

    public Module Module { get; }

    public IntPtr FilesLines => Interlocked.CompareExchange(ref _filesLines, IntPtr.Zero, IntPtr.Zero);

    internal int AllocatedByteLength => Volatile.Read(ref _allocatedByteLength);

    public void Dispose()
    {
        var filesLines = Interlocked.Exchange(ref _filesLines, IntPtr.Zero);
        if (filesLines != IntPtr.Zero)
        {
            var byteLength = Volatile.Read(ref _allocatedByteLength);
            _strategy.Free(filesLines, _origin);
            Volatile.Write(ref _allocatedByteLength, 0);
            _strategy.Diagnostics.OnFreed(_origin, byteLength);
        }

        GC.SuppressFinalize(this);
    }
}
