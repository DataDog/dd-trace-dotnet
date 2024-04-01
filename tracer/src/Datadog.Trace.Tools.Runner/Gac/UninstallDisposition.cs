// <copyright file="UninstallDisposition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Gac;

// Code based on: https://github.com/dotnet/pinvoke/tree/main/src/Fusion

internal enum UninstallDisposition
{
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_UNINSTALLED = 1,
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_STILL_IN_USE = 2,
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_ALREADY_UNINSTALLED = 3,
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_DELETE_PENDING = 4,
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_HAS_INSTALL_REFERENCES = 5,
    IASSEMBLYCACHE_UNINSTALL_DISPOSITION_REFERENCE_NOT_FOUND = 6,
}
