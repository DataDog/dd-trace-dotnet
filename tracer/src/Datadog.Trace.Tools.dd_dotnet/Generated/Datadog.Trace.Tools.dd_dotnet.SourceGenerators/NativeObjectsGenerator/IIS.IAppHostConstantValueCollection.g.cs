﻿
using System;
using System.Runtime.InteropServices;

namespace NativeObjects;

internal unsafe class IAppHostConstantValueCollection : Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostConstantValueCollection
{
    public static IAppHostConstantValueCollection Wrap(IntPtr obj) => new IAppHostConstantValueCollection(obj);

    private readonly IntPtr _implementation;

    public IAppHostConstantValueCollection(IntPtr implementation)
    {
        _implementation = implementation;
    }

    private nint* VTable => (nint*)*(nint*)_implementation;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_implementation != IntPtr.Zero)
        {
            Release();
        }
    }

    ~IAppHostConstantValueCollection()
    {
        Dispose();
    }

    public int QueryInterface(in System.Guid a0, out nint a1)
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, in System.Guid, out nint, int>)*(VTable + 0);
        var result = func(_implementation, in a0, out a1);
        return result;
    }
    public int AddRef()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, int>)*(VTable + 1);
        var result = func(_implementation);
        return result;
    }
    public int Release()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, int>)*(VTable + 2);
        var result = func(_implementation);
        return result;
    }
    public int Count()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out int, int>)*(VTable + 3);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostConstantValue GetConstantValue(Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant a0)
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant, out IntPtr, int>)*(VTable + 4);
        var result = func(_implementation, a0, out var returnptr);
        var returnvalue = NativeObjects.IAppHostConstantValue.Wrap(returnptr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }


}
