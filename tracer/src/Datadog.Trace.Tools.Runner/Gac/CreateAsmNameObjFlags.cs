// <copyright file="CreateAsmNameObjFlags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Tools.Runner.Gac;

[Flags]
internal enum CreateAsmNameObjFlags
{
    /// <summary>
    /// If this flag is specified, the szAssemblyName parameter of CreateAssemblyNameObject is a fully-specified
    /// side-by-side assembly name and is parsed to the individual properties.
    /// </summary>
    CANOF_PARSE_DISPLAY_NAME = 0x1,

    /// <summary>
    /// Reserved.
    /// </summary>
    CANOF_SET_DEFAULT_VALUES = 0x2,
    CANOF_VERIFY_FRIEND_ASSEMBLYNAME = 0x4,
    CANOF_PARSE_FRIEND_DISPLAY_NAME = (CANOF_PARSE_DISPLAY_NAME | CANOF_VERIFY_FRIEND_ASSEMBLYNAME)
}
