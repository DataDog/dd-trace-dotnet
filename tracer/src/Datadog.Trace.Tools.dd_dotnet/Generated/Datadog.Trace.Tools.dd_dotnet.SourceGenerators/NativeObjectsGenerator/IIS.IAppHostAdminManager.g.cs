﻿
using System;
using System.Runtime.InteropServices;

namespace NativeObjects;

internal unsafe class IAppHostAdminManager : Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostAdminManager
{
    public static IAppHostAdminManager Wrap(IntPtr obj) => new IAppHostAdminManager(obj);

    private readonly IntPtr _implementation;

    public IAppHostAdminManager(IntPtr implementation)
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

    ~IAppHostAdminManager()
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
    public Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostElement GetAdminSection(string a0, string a1)
    {
        var str0 = Marshal.StringToBSTR(a0);
        var str1 = Marshal.StringToBSTR(a1);
        var func = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, out IntPtr, int>)*(VTable + 3);
        var result = func(_implementation, str0, str1, out var returnptr);
        var returnvalue = NativeObjects.IAppHostElement.Wrap(returnptr);
        Marshal.FreeBSTR(str0);
        Marshal.FreeBSTR(str1);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public nint GetMetadata(string a0)
    {
        var str0 = Marshal.StringToBSTR(a0);
        var func = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, out nint, int>)*(VTable + 4);
        var result = func(_implementation, str0, out var returnvalue);
        Marshal.FreeBSTR(str0);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public void SetMetadata(string a0, Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant a1)
    {
        var str0 = Marshal.StringToBSTR(a0);
        var func = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant, int>)*(VTable + 5);
        var result = func(_implementation, str0, a1);
        Marshal.FreeBSTR(str0);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
    }
    public Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostConfigManager GetConfigManager()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 6);
        var result = func(_implementation, out var returnptr);
        var returnvalue = NativeObjects.IAppHostConfigManager.Wrap(returnptr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }


}
