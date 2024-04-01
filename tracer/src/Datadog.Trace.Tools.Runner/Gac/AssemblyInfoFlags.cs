// <copyright file="AssemblyInfoFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

[Flags]
internal enum AssemblyInfoFlags
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0x0,

    /// <summary>
    /// Indicates that the assembly is installed. The current version of the .NET Framework always sets dwAssemblyFlags to this value.
    /// </summary>
    ASSEMBLYINFO_FLAG_INSTALLED = 0x1,

    /// <summary>
    /// Indicates that the assembly is a payload resident. The current version of the .NET Framework never sets dwAssemblyFlags to this value.
    /// </summary>
    ASSEMBLYINFO_FLAG_PAYLOADRESIDENT = 0x2,
}
