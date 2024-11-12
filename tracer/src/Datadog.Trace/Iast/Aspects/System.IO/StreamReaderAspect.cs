// <copyright file="StreamReaderAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> StreamReaderAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class StreamReaderAspect
{
    /// <summary>
     /// Launches a path traversal vulnerability if the file is tainted
     /// </summary>
     /// <param name="path">the path of the file</param>
     /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.Text.Encoding)", 1)]
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean)", 2)]
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean,System.Int32)", 3)]
#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean,System.IO.FileStreamOptions)", 3)]
    [AspectMethodInsertBefore("System.IO.StreamReader::.ctor(System.String,System.IO.FileStreamOptions)", 1)]
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
            IastModule.Log.Error(ex, $"Error invoking {nameof(StreamReaderAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
