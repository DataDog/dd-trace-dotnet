// <copyright file="FileAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> File class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class FileAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path or file</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.File::Create(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::CreateText(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::Delete(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::OpenRead(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::OpenText(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::OpenWrite(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::ReadAllBytes(System.String)")]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::ReadAllBytesAsync(System.String,System.Threading.CancellationToken)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.File::ReadAllLines(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::ReadAllLines(System.String,System.Text.Encoding)", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::ReadAllLinesAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.IO.File::ReadAllLinesAsync(System.String,System.Text.Encoding,System.Threading.CancellationToken)", 2)]
#endif
    [AspectMethodInsertBefore("System.IO.File::ReadAllText(System.String)")]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::ReadAllTextAsync(System.String,System.Text.Encoding,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.IO.File::ReadAllTextAsync(System.String,System.Threading.CancellationToken)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.File::ReadLines(System.String)")]
#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.File::ReadLinesAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.IO.File::ReadLinesAsync(System.String,System.Text.Encoding,System.Threading.CancellationToken)", 2)]
#endif
    [AspectMethodInsertBefore("System.IO.File::AppendAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>)", 1)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding)", 2)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllText(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllText(System.String,System.String,System.Text.Encoding)", 2)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::AppendAllTextAsync(System.String,System.String,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllTextAsync(System.String,System.String,System.Text.Encoding,System.Threading.CancellationToken)", 3)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.IO.File::AppendAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding,System.Threading.CancellationToken)", 3)]
#endif
    [AspectMethodInsertBefore("System.IO.File::AppendText(System.String)")]
    [AspectMethodInsertBefore("System.IO.File::ReadLines(System.String,System.Text.Encoding)", 1)]
    [AspectMethodInsertBefore("System.IO.File::ReadAllText(System.String,System.Text.Encoding)", 1)]
    [AspectMethodInsertBefore("System.IO.File::ReadLines(System.String,System.Text.Encoding)", 1)]
    [AspectMethodInsertBefore("System.IO.File::Create(System.String,System.Int32)", 1)]
    [AspectMethodInsertBefore("System.IO.File::Create(System.String,System.Int32,System.IO.FileOptions)", 2)]
#if NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::Create(System.String,System.Int32,System.IO.FileOptions,System.Security.AccessControl.FileSecurity)", 3)]
#endif
    [AspectMethodInsertBefore("System.IO.File::Open(System.String,System.IO.FileMode)", 1)]
    [AspectMethodInsertBefore("System.IO.File::Open(System.String,System.IO.FileMode,System.IO.FileAccess)", 2)]
    [AspectMethodInsertBefore("System.IO.File::Open(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare)", 3)]
#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.File::Open(System.String,System.IO.FileStreamOptions)", 1)]
    [AspectMethodInsertBefore("System.IO.File::OpenHandle(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.IO.FileOptions,System.Int64)", 5)]
#endif
    [AspectMethodInsertBefore("System.IO.File::SetAttributes(System.String,System.IO.FileAttributes)", 1)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllBytes(System.String,System.Byte[])", 1)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::WriteAllBytesAsync(System.String,System.Byte[],System.Threading.CancellationToken)", 2)]
#endif
    [AspectMethodInsertBefore("System.IO.File::WriteAllLines(System.String,System.String[])", 1)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllLines(System.String,System.String[],System.Text.Encoding)", 2)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>)", 1)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding)", 2)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::WriteAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding,System.Threading.CancellationToken)", 3)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Threading.CancellationToken)", 2)]
#endif
    [AspectMethodInsertBefore("System.IO.File::WriteAllText(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllText(System.String,System.String,System.Text.Encoding)", 2)]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::WriteAllTextAsync(System.String,System.String,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.IO.File::WriteAllTextAsync(System.String,System.String,System.Text.Encoding,System.Threading.CancellationToken)", 3)]
#endif
    [AspectMethodInsertBefore("System.IO.File::Copy(System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.IO.File::Copy(System.String,System.String,System.Boolean)", new int[] { 1, 2 })]
    [AspectMethodInsertBefore("System.IO.File::Move(System.String,System.String)", new int[] { 0, 1 })]
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.IO.File::Move(System.String,System.String,System.Boolean)", new int[] { 1, 2 })]
#endif
    [AspectMethodInsertBefore("System.IO.File::Replace(System.String,System.String,System.String)", new int[] { 0, 1, 2 })]
    [AspectMethodInsertBefore("System.IO.File::Replace(System.String,System.String,System.String,System.Boolean)", new int[] { 1, 2, 3 })]
    public static string ReviewPath(string path)
    {
        try
        {
            VulnerabilitiesModule.OnPathTraversal(path);
            return path;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(FileAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
