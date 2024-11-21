// <copyright file="TargetFrameworks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler;

[Flags]
internal enum TargetFrameworks : uint
{
    // NOTE : When modifying this file make sure to update the TargetFrameworks enum in the CodeGenerator folder inside the _build project

    NET461 = 1,
    NET462 = 2,
    NETSTANDARD2_0 = 4,
    NETCOREAPP2_1 = 8,
    NETCOREAPP3_0 = 16,
    NETCOREAPP3_1 = 32,
    NET5_0 = 64,
    NET6_0 = 128,
    NET7_0 = 256,
    NET8_0 = 512,
    NET9_0 = 1024,
}
