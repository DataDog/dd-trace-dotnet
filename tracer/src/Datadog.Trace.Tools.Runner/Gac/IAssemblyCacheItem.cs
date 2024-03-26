// <copyright file="IAssemblyCacheItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("9e3aaeb4-d1cd-11d2-bab9-00c04f8eceae")]
internal interface IAssemblyCacheItem
{
    void CreateStream([MarshalAs(UnmanagedType.LPWStr)] string pszName, uint dwFormat, uint dwFlags, uint dwMaxSize, out IStream ppStream);

    void IsNameEqual(IAssemblyName pName);

    void Commit(uint dwFlags);

    void MarkAssemblyVisible(uint dwFlags);
}
