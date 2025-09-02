// <copyright file="StdTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.LibDatadog.Logging;

/// <summary>
/// Represents the target stream for a standard output/error logger.
/// </summary>
internal enum StdTarget
{
    /// <summary>
    /// The standard output stream.
    /// </summary>
    Stdout = 0,

    /// <summary>
    /// The standard error stream.
    /// </summary>
    Stderr = 1,
}
