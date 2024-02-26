// <copyright file="FileCoverageMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Coverage.Metadata;

/// <summary>
/// File coverage metadata struct
/// </summary>
public readonly struct FileCoverageMetadata
{
    /// <summary>
    /// File path
    /// </summary>
    public readonly string Path;

    /// <summary>
    /// Last executable line number
    /// </summary>
    public readonly int LastExecutableLine;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCoverageMetadata"/> struct.
    /// </summary>
    /// <param name="path">File path</param>
    /// <param name="lastExecutableLine">Last executable line number</param>
    public FileCoverageMetadata(string path, int lastExecutableLine)
    {
        Path = path;
        LastExecutableLine = lastExecutableLine;
    }
}
