// <copyright file="TargetFrameworkInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler;

internal static class TargetFrameworkInfo
{
#if NET6_0
    public const TargetFrameworks Current = TargetFrameworks.NET6_0;
#elif NETCOREAPP3_1
    public const TargetFrameworks Current = TargetFrameworks.NETCOREAPP3_1;
#elif NETSTANDARD2_0
    public const TargetFrameworks Current = TargetFrameworks.NETSTANDARD2_0;
#elif NETFRAMEWORK
    public const TargetFrameworks Current = TargetFrameworks.NET461;
#else
#error Unexpected TFM
#endif
}
