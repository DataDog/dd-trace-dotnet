// <copyright file="PlatformKeys.DotNet.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    /// <summary>
    /// Platform key indicating the path to the .NET Core CLR profiler path
    /// </summary>
    public const string DotNetCoreClrProfiler = "CORECLR_PROFILER_PATH";

    /// <summary>
    /// Platform key indicating the path to the .NET Framework CLR profiler path
    /// </summary>
    public const string DotNetClrProfiler = "COR_PROFILER_PATH";
}
