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
    IASSEMBLYCACHE_INSTALL_FLAG_REFRESH = 0x1,
    IASSEMBLYCACHE_INSTALL_FLAG_FORCE_REFRESH = 0x2,
}
