// <copyright file="IAssemblyEnum.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Gac;

[Guid("21b8916c-f28e-11d2-a473-00c04f8ef448")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAssemblyEnum
{
    Hresult GetNextAssembly(IntPtr pvReserved, out IAssemblyName ppName, int dwFlags);

    Hresult Reset();

    Hresult Clone(out IAssemblyEnum ppEnum);
}
