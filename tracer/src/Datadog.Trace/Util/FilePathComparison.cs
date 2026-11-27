// <copyright file="FilePathComparison.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util;

/// <summary>
/// Compares file system paths the way the current OS usually does: case-insensitive on Windows, case-sensitive elsewhere.
/// </summary>
internal static class FilePathComparison
{
    /// <summary>
    /// Gets the comparer for file system paths on the current OS.
    /// </summary>
    public static readonly StringComparer Comparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Gets the comparison for file system paths on the current OS.
    /// </summary>
    public static readonly StringComparison Comparison = FrameworkDescription.Instance.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
