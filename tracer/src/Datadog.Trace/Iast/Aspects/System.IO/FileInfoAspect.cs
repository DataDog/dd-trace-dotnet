// <copyright file="FileInfoAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> FileInfoAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class FileInfoAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path or file</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.FileInfo::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.IO.FileInfo::CopyTo(System.String)")]
    [AspectMethodInsertBefore("System.IO.FileInfo::CopyTo(System.String,System.Boolean)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.FileInfo::MoveTo(System.String,System.Boolean)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.FileInfo::MoveTo(System.String)")]
    [AspectMethodInsertBefore("System.IO.FileInfo::Replace(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.FileInfo::Replace(System.String,System.String,System.Boolean)", new int[] { 1, 2 })]
    public static string ReviewPath(string path)
    {
        try
        {
            VulnerabilitiesModule.OnPathTraversal(path);
            return path;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(FileInfoAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
