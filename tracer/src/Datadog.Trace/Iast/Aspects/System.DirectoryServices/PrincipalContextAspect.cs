// <copyright file="PrincipalContextAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.DirectoryServices;

/// <summary> DirectorySearcher class aspects </summary>
[AspectClass("System.DirectoryServices.AccountManagement", [AspectFilter.StringOptimization])]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]

public partial class PrincipalContextAspect
{
    /// <summary>
    /// PrincipalContext instrumentated method
    /// </summary>
    /// <param name="path"> sensitive string to analyze </param>
    /// <returns> the string parameter </returns>
    [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.DirectoryServices.AccountManagement.ContextOptions)", 1)]
    [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.DirectoryServices.AccountManagement.ContextOptions,System.String,System.String)", 3)]
    public static object Init(string path)
    {
        try
        {
            IastModule.OnLdapInjection(path);
            return path;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(PrincipalContextAspect)}.{nameof(Init)}");
            return path;
        }
    }
}
