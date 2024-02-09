// <copyright file="AssemblyCacheContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Gac;

internal sealed class AssemblyCacheContainer : IDisposable
{
    private readonly IntPtr _libPointer;

    public AssemblyCacheContainer(IntPtr libPointer, IAssemblyCache assemblyCache)
    {
        _libPointer = libPointer;
        AssemblyCache = assemblyCache;
    }

    public IAssemblyCache AssemblyCache { get; private set; }

    public void Dispose()
    {
        if (_libPointer != IntPtr.Zero)
        {
            NativeLibrary.Free(_libPointer);
        }
    }
}
