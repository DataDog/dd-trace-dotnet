// <copyright file="GacNativeMethods.Impl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Datadog.Trace.Tools.Runner.Gac;

internal sealed partial class GacNativeMethods
{
    private delegate Hresult CreateAssemblyCacheDelegate(out IAssemblyCache ppAsmCache, IntPtr reserved);

    private delegate Hresult CreateAssemblyNameObjectDelegate(out IAssemblyName ppAssemblyNameObj, [MarshalAs(UnmanagedType.LPWStr)] string szAssemblyName, CreateAsmNameObjFlags flags,  IntPtr pvReserved);

    private delegate Hresult CreateAssemblyEnumDelegate(out IAssemblyEnum pEnum, IntPtr pUnkReserved, IAssemblyName pName, AsmCacheFlags dwFlags, IntPtr pvReserved);

    internal IAssemblyCache CreateAssemblyCache()
    {
        var createAssemblyCachePointer = NativeLibrary.GetExport(GetPointer(), nameof(CreateAssemblyCache));
        var createAssemblyCache = Marshal.GetDelegateForFunctionPointer<CreateAssemblyCacheDelegate>(createAssemblyCachePointer);
        var hr = createAssemblyCache(out var ppAsmCache, IntPtr.Zero);
        if (hr != Hresult.S_OK)
        {
            throw new TargetInvocationException($"Error creating AssemblyCache. HRESULT = {hr}", null);
        }

        return ppAsmCache;
    }

    internal IAssemblyName CreateAssemblyName(string assemblyName)
    {
        var createAssemblyNameObjectPointer = NativeLibrary.GetExport(GetPointer(), "CreateAssemblyNameObject");
        var createAssemblyNameObject = Marshal.GetDelegateForFunctionPointer<CreateAssemblyNameObjectDelegate>(createAssemblyNameObjectPointer);
        var hr = createAssemblyNameObject(out var ppAsmName, assemblyName, 0, IntPtr.Zero);
        if (hr != Hresult.S_OK)
        {
            throw new TargetInvocationException($"Error creating AssemblyNameObject. HRESULT = {hr}", null);
        }

        return ppAsmName;
    }

    internal ICollection<AssemblyName> GetAssemblyNames(string assemblyName)
    {
        var createAssemblyEnumPointer = NativeLibrary.GetExport(GetPointer(), "CreateAssemblyEnum");
        var createAssemblyEnum = Marshal.GetDelegateForFunctionPointer<CreateAssemblyEnumDelegate>(createAssemblyEnumPointer);
        var pAssemblyName = CreateAssemblyName(assemblyName);
        var hr = createAssemblyEnum(out var pAssemblyEnum, IntPtr.Zero, pAssemblyName, AsmCacheFlags.ASM_CACHE_GAC, IntPtr.Zero);
        if (hr != Hresult.S_OK)
        {
            return [];
        }

        var lstAsmName = new List<AssemblyName>();
        while (pAssemblyEnum.GetNextAssembly(IntPtr.Zero, out pAssemblyName, 0) == 0 && pAssemblyName != null)
        {
            var nSize = 260;
            var sbDisplayName = new StringBuilder(nSize);
            hr = pAssemblyName.GetDisplayName(sbDisplayName, ref nSize, AsmDisplayFlags.ASM_DISPLAYF_FULL);
            if (hr == Hresult.S_OK)
            {
                lstAsmName.Add(new AssemblyName(sbDisplayName.ToString()));
            }
        }

        return lstAsmName;
    }
}
