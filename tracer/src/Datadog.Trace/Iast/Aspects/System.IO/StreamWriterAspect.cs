// <copyright file="StreamWriterAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> StreamWriterAspect class aspects </summary>
[AspectClass("mscorlib,System.Private.CoreLib", AspectType.Sink, VulnerabilityType.PathTraversal)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public partial class StreamWriterAspect
{
    /// <summary>
    /// Launches a path traversal vulnerability if the file is tainted
    /// </summary>
    /// <param name="path">the path or file</param>
    /// <returns>the path parameter</returns>
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String, System.Text.Encoding, System.IO.FileStreamOptions)", 2)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean)", 1)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding)", 2)]
    [AspectMethodInsertBefore("System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding,System.Int32)", 3)]
    public static string Init(string path)
    {
        IastModule.OnPathTraversal(path);
        return path;
    }
}
