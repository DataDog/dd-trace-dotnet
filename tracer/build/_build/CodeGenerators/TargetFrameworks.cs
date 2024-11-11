// <copyright file="TargetFrameworks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace CodeGenerators;

[Flags]
internal enum TargetFrameworks : uint
{
    None = 0, // For CodeGenerator only

    // NOTE : When modifying this file make sure to update the TargetFrameworks enum in the ClrProfiler folder inside the Datadog.Trace project

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
    NET0_0 = 1024,
}


internal static class TargetFrameworksExtensions
{ 
    public static bool IsNetFxOnly(this TargetFrameworks tfm)
    {
        return ((uint)(tfm & (~(TargetFrameworks.NET461 | TargetFrameworks.NET462)))) == 0;
    }
}
