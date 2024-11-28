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
    NETSTANDARD2_0 = 2,
    NETCOREAPP3_1 = 4,
    NET6_0 = 8,
}


internal static class TargetFrameworksExtensions
{ 
    public static bool IsNetFxOnly(this TargetFrameworks tfm)
    {
        return tfm == TargetFrameworks.NET461;
    }
}
