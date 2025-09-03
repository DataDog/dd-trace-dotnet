// <copyright file="LibraryConfigsWithLogs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

[StructLayout(LayoutKind.Sequential)]
internal struct LibraryConfigsWithLogs
{
    public LibraryConfigs Configs;    // Your existing configs type
    public CString Logs;        // The debug messages
}
