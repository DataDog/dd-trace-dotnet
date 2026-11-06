// <copyright file="ModuleEnum.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

internal sealed unsafe class ModuleEnum : ICorProfilerModuleEnum
{
    private readonly nint[] _ids;
    private int _position;

    public ModuleEnum(nint[] ids)
    {
        _ids = ids;
    }

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef() => 1;

    public int Release() => 1;

    public HResult Skip(uint celt)
    {
        _position = Math.Min(_ids.Length, _position + (int)celt);
        return HResult.S_OK;
    }

    public HResult Reset()
    {
        _position = 0;
        return HResult.S_OK;
    }

    public HResult Clone(out void* ppEnum)
    {
        var wrapper = NativeObjects.ICorProfilerModuleEnum.Wrap(new ModuleEnum(_ids));
        NativeObjectRoots.Add(wrapper);
        ppEnum = (void*)(IntPtr)wrapper;
        return HResult.S_OK;
    }

    public HResult GetCount(out uint pcelt)
    {
        pcelt = (uint)_ids.Length;
        return HResult.S_OK;
    }

    public HResult Next(uint celt, ModuleId* moduleIds, out uint pceltFetched)
    {
        var count = 0;
        while (count < celt && _position < _ids.Length)
        {
            moduleIds[count++] = new ModuleId(_ids[_position++]);
        }

        pceltFetched = (uint)count;
        return count == celt ? HResult.S_OK : HResult.S_FALSE;
    }
}
#endif
