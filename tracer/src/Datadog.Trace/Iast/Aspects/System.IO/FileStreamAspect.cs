// <copyright file="FileStreamAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> StreamWriterAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class FileStreamAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path of the file</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode)", 1)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess)", 2)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare)", 3)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32)", 4)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32,System.Boolean)", 5)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32,System.IO.FileOptions)", 5)]
#if NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.Security.AccessControl.FileSystemRights,System.IO.FileShare,System.Int32,System.IO.FileOptions)", 5)]
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.Security.AccessControl.FileSystemRights,System.IO.FileShare,System.Int32,System.IO.FileOptions,System.Security.AccessControl.FileSecurity)", 6)]
#endif
#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.FileStream::.ctor(System.String,System.IO.FileStreamOptions)", 1)]
#endif
    public static string ReviewPath(string path)
    {
        try
        {
            VulnerabilitiesModule.OnPathTraversal(path);
            return path;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(FileStreamAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
