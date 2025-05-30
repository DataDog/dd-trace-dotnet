// <copyright file="AsmCacheFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Gac;

internal enum AsmCacheFlags
{
    ASM_CACHE_ZAP = 0x01,
    ASM_CACHE_GAC = 0x02,
    ASM_CACHE_DOWNLOAD = 0x04,
    ASM_CACHE_ROOT = 0x08,
    ASM_CACHE_ROOT_EX = 0x80
}
