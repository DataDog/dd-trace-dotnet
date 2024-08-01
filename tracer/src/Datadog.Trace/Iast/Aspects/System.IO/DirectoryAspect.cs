// <copyright file="DirectoryAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> DirectoryAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DirectoryAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the directory is tainted
    /// </summary>
    /// <param name="path">the path</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.Directory::CreateDirectory(System.String)")]

#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.Directory::CreateDirectory(System.String,System.IO.UnixFileMode)", 1)]
    [AspectMethodInsertBefore("System.IO.Directory::CreateTempSubdirectory(System.String)")]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::Delete(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::Delete(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.IO.Directory::GetDirectories(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::GetDirectories(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::GetDirectories(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::GetDirectories(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::GetDirectoryRoot(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::GetFiles(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::GetFiles(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::GetFiles(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::GetFiles(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::GetFileSystemEntries(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::GetFileSystemEntries(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::GetFileSystemEntries(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::GetFileSystemEntries(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::Move(System.String,System.String)", new int[] { 0, 1 })]
#if NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::CreateDirectory(System.String,System.Security.AccessControl.DirectorySecurity)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::SetAccessControl(System.String,System.Security.AccessControl.DirectorySecurity)", 1)]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateDirectories(System.String)")]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateDirectories(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateDirectories(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateDirectories(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFiles(System.String)")]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFiles(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFiles(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFiles(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFileSystemEntries(System.String)")]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String,System.IO.SearchOption)", new int[] { 1, 2 })]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String,System.IO.EnumerationOptions)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.Directory::SetCurrentDirectory(System.String)")]
    public static string ReviewPath(string path)
    {
        try
        {
            VulnerabilitiesModule.OnPathTraversal(path);
            return path;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DirectoryAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
