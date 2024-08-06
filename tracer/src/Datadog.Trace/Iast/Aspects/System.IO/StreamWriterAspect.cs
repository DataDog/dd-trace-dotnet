// <copyright file="StreamWriterAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> StreamWriterAspect class aspects </summary>
[AspectClass("mscorlib,System.IO.FileSystem,System.Runtime", AspectType.RaspIastSink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class StreamWriterAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path of the file</param>
    /// <returns>the path parameter</returns>
#if NET6_0_OR_GREATER
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Text.Encoding,System.IO.FileStreamOptions)", 2)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.IO.FileStreamOptions)", 1)]
#endif
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding)", 2)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding,System.Int32)", 3)]
    public static string ReviewPath(string path)
    {
        try
        {
            VulnerabilitiesModule.OnPathTraversal(path);
            return path;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(StreamWriterAspect)}.{nameof(ReviewPath)}");
            return path;
        }
    }
}
