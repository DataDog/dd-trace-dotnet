// <copyright file="InstrumentationCategory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler;

[Flags]
internal enum InstrumentationCategory : uint
{
    None = 0,
    Tracing = 1,
    AppSec = 2,
    Iast = 4,
    Rasp = 8,

    IastRasp = Iast | Rasp,
}
