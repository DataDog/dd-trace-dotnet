// <copyright file="InstrumentationCategory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace CodeGenerators;

[Flags]
internal enum InstrumentationCategory : uint
{
    // NOTE : When modifying this file make sure to update the InstrumentationCategory enum in the ClrProfiler folder inside the Datadog.Trace project

    Tracing = 1,
    AppSec = 2,
    Iast = 4,
    Rasp = 8,

    IastRasp = Iast | Rasp,
}
