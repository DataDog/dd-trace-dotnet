// <copyright file="FileConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.InteropServices;

namespace Datadog.Trace.LibDatadog;

/// <summary>
/// Represents a configuration for a file logger.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FileConfig
{
    /// <summary>
    /// The path to the log file.
    /// If the intermediate directory does not exist, it will be created while configuring the logger.
    /// </summary>
    public CharSlice Path;

    /// <summary>
    /// The maximum total number of files (current + rotated) to keep on disk.
    /// When this limit is exceeded, the oldest rotated files are deleted.
    /// Set to 0 to disable file cleanup.
    /// </summary>
    public ulong MaxFiles;

    /// <summary>
    /// The maximum size in bytes for each log file.
    /// Set to 0 to disable size-based rotation.
    /// </summary>
    public ulong MaxSizeBytes;
}
