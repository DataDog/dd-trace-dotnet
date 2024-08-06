// <copyright file="DirectorySearcherAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.DirectoryServices;

/// <summary> DirectorySearcher class aspects </summary>
[AspectClass("System.DirectoryServices", [AspectFilter.StringOptimization])]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]

public partial class DirectorySearcherAspect
{
    /// <summary>
    /// DirectorySearcher instrumentated method
    /// </summary>
    /// <param name="path"> sensitive string to analyze </param>
    /// <returns> the string parameter </returns>
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::set_Filter(System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String,System.String[])", 1)]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String,System.String[],System.DirectoryServices.SearchScope)", 2)]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String,System.String[])", 1)]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String,System.String[],System.DirectoryServices.SearchScope)", 2)]
    public static object Init(string path)
    {
        try
        {
            IastModule.OnLdapInjection(path);
            return path;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DirectorySearcherAspect)}.{nameof(Init)}");
            return path;
        }
    }
}
