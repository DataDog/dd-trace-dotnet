// <copyright file="AssemblyCacheInstallFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

[Flags]
internal enum AssemblyCacheInstallFlags
{
    None = 0x0,

    /// <summary>
    /// Replace existing files in the side-by-side store with the files in the assembly being installed
    /// if the version of the file in the assembly is greater than or equal to the version of the existing file.
    /// </summary>
    IASSEMBLYCACHE_INSTALL_FLAG_REFRESH = 0x1,

    /// <summary>
    /// Replace existing files in the side-by-side store with the files in the assembly being installed.
    /// </summary>
    IASSEMBLYCACHE_INSTALL_FLAG_FORCE_REFRESH = 0x2,
}
