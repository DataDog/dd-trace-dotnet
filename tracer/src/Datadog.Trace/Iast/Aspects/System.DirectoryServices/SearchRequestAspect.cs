// <copyright file="SearchRequestAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.DirectoryServices;

/// <summary> SearchRequest class aspects </summary>
[AspectClass("System.DirectoryServices.Protocols", AspectFilter.StringOptimization)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]

public partial class SearchRequestAspect
{
    /// <summary>
    /// SearchRequest instrumentated method
    /// </summary>
    /// <param name="path"> sensitive string to analyze </param>
    /// <returns> the string parameter </returns>
    [AspectMethodInsertBefore("System.DirectoryServices.Protocols.SearchRequest::.ctor(System.String,System.String,System.DirectoryServices.Protocols.SearchScope,System.String[])", 2)]
    public static object Init(string path)
    {
        IastModule.OnLdapInjection(path);
        return path;
    }
}
