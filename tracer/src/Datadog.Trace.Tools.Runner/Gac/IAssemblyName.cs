// <copyright file="IAssemblyName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
internal interface IAssemblyName
{
    [PreserveSig]
    Hresult SetProperty(int propertyId, IntPtr pvProperty, int cbProperty);

    [PreserveSig]
    Hresult GetProperty(int propertyId, out IntPtr pvProperty, ref int pcbProperty);

    [PreserveSig]
#pragma warning disable CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation
    Hresult Finalize();
#pragma warning restore CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation

    [PreserveSig]
    Hresult GetDisplayName(StringBuilder szDisplayName, ref int pccDisplayName, AsmDisplayFlags dwDisplayFlags);

    [PreserveSig]
    Hresult BindToObject(
        object /*REFIID*/ refIID,
        object /*IAssemblyBindSink*/ pAsmBindSink,
        IApplicationContext pApplicationContext,
        [MarshalAs(UnmanagedType.LPWStr)] string szCodeBase,
        long llFlags,
        int pvReserved,
        uint cbReserved,
        out int ppv);

    [PreserveSig]
    Hresult GetName(ref int lpcwBuffer, StringBuilder pwzName);

    [PreserveSig]
    Hresult GetVersion(out int pdwVersionHi, out int pdwVersionLow);

    [PreserveSig]
    Hresult IsEqual(IAssemblyName pName, int dwCmpFlags);

    [PreserveSig]
    Hresult Clone(out IAssemblyName pName);
}
