// <copyright file="AsmDisplayFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Gac;

internal enum AsmDisplayFlags
{
    ASM_DISPLAYF_VERSION = 0x1,
    ASM_DISPLAYF_CULTURE = 0x2,
    ASM_DISPLAYF_PUBLIC_KEY_TOKEN = 0x4,
    ASM_DISPLAYF_PUBLIC_KEY = 0x8,
    ASM_DISPLAYF_CUSTOM = 0x10,
    ASM_DISPLAYF_PROCESSORARCHITECTURE = 0x20,
    ASM_DISPLAYF_LANGUAGEID = 0x40,
    ASM_DISPLAYF_RETARGET = 0x80,
    ASM_DISPLAYF_CONFIG_MASK = 0x100,
    ASM_DISPLAYF_MVID = 0x200,
    ASM_DISPLAYF_CONTENT_TYPE = 0x400,
    ASM_DISPLAYF_FULL = (((((ASM_DISPLAYF_VERSION | ASM_DISPLAYF_CULTURE) | ASM_DISPLAYF_PUBLIC_KEY_TOKEN) | ASM_DISPLAYF_RETARGET) | ASM_DISPLAYF_PROCESSORARCHITECTURE) | ASM_DISPLAYF_CONTENT_TYPE)
}
