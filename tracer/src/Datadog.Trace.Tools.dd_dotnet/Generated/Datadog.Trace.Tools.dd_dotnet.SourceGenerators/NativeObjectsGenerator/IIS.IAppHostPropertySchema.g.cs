﻿
using System;
using System.Runtime.InteropServices;

namespace NativeObjects;

internal unsafe class IAppHostPropertySchema : Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostPropertySchema
{
    public static IAppHostPropertySchema Wrap(IntPtr obj) => new IAppHostPropertySchema(obj);

    private readonly IntPtr _implementation;

    public IAppHostPropertySchema(IntPtr implementation)
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

    ~IAppHostPropertySchema()
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
    public string Name()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 3);
        var result = func(_implementation, out var returnstr);
        var returnvalue = Marshal.PtrToStringBSTR(returnstr);
        Marshal.FreeBSTR(returnstr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public string Type()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 4);
        var result = func(_implementation, out var returnstr);
        var returnvalue = Marshal.PtrToStringBSTR(returnstr);
        Marshal.FreeBSTR(returnstr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public object DefaultValue()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out object, int>)*(VTable + 5);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsRequired()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 6);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsUniqueKey()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 7);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsCombinedKey()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 8);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsExpanded()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 9);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public string ValidationType()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 10);
        var result = func(_implementation, out var returnstr);
        var returnvalue = Marshal.PtrToStringBSTR(returnstr);
        Marshal.FreeBSTR(returnstr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public string ValidationParameter()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 11);
        var result = func(_implementation, out var returnstr);
        var returnvalue = Marshal.PtrToStringBSTR(returnstr);
        Marshal.FreeBSTR(returnstr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant GetMetadata(string a0)
    {
        var str0 = Marshal.StringToBSTR(a0);
        var func = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, out Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.Variant, int>)*(VTable + 12);
        var result = func(_implementation, str0, out var returnvalue);
        Marshal.FreeBSTR(str0);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsCaseSensitive()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 13);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public Datadog.Trace.Tools.dd_dotnet.Checks.Windows.IIS.IAppHostConstantValueCollection PossibleValues()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 14);
        var result = func(_implementation, out var returnptr);
        var returnvalue = NativeObjects.IAppHostConstantValueCollection.Wrap(returnptr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool DoesAllowInfinite()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 15);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public bool IsEncrypted()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out bool, int>)*(VTable + 16);
        var result = func(_implementation, out var returnvalue);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }
    public string TimeSpanFormat()
    {
        var func = (delegate* unmanaged[Stdcall]<IntPtr, out IntPtr, int>)*(VTable + 17);
        var result = func(_implementation, out var returnstr);
        var returnvalue = Marshal.PtrToStringBSTR(returnstr);
        Marshal.FreeBSTR(returnstr);
        if (result != 0)
        {
            throw new System.ComponentModel.Win32Exception(result);
        }
        return returnvalue;
    }


}
