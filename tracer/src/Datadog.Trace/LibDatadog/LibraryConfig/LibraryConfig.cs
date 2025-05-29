// <copyright file="LibraryConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.LibraryConfig;

[StructLayout(LayoutKind.Sequential)]
internal struct LibraryConfig
{
    public LibraryConfigName Name; // enum ddog_LibraryConfigName
    public CString Value;
    public LibraryConfigSource Source; // enum ddog_LibraryConfigSource
}
