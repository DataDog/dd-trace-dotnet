// <copyright file="IApplicationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("7c23ff90-33af-11d3-95da-00a024a85b51")]
internal interface IApplicationContext
{
    void SetContextNameObject(IAssemblyName pName);

    void GetContextNameObject(out IAssemblyName ppName);

    void Set([MarshalAs(UnmanagedType.LPWStr)] string szName, int pvValue, uint cbValue, uint dwFlags);

    void Get([MarshalAs(UnmanagedType.LPWStr)] string szName, out int pvValue, ref uint pcbValue, uint dwFlags);

    void GetDynamicDirectory(out int wzDynamicDir, ref uint pdwSize);
}
