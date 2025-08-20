// <copyright file="StdConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog.Logging;

/// <summary>
/// Represents a configuration for a standard output/error logger.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StdConfig
{
    public StdTarget Target;
}
