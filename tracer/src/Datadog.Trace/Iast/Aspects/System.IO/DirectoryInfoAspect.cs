// <copyright file="DirectoryInfoAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> DirectoryInfoAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DirectoryInfoAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::CreateSubdirectory(System.String)")]
#if NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::CreateSubdirectory(System.String,System.Security.AccessControl.DirectorySecurity)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::MoveTo(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFileSystemInfos(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFileSystemInfos(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFileSystemInfos(System.String,System.IO.EnumerationOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFiles(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFiles(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetFiles(System.String,System.IO.EnumerationOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetDirectories(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetDirectories(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::GetDirectories(System.String,System.IO.EnumerationOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String,System.IO.EnumerationOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFiles(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFiles(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateFiles(System.String,System.IO.EnumerationOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateDirectories(System.String)")]
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.SearchOption)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.EnumerationOptions)", 1)]
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
            IastModule.Log.Error(ex, $"Error invoking {nameof(DirectoryInfoAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
