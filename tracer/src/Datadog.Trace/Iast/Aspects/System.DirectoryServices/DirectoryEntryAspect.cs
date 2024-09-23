// <copyright file="DirectoryEntryAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.DirectoryServices;

/// <summary> DirectoryEntry class aspects </summary>
[AspectClass("System.DirectoryServices", [AspectFilter.StringOptimization])]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public partial class DirectoryEntryAspect
{
    /// <summary>
    /// DirectoryEntry instrumentated method
    /// </summary>
    /// <param name="path"> sensitive string to analyze </param>
    /// <returns> the string parameter </returns>
    [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::set_Path(System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String)")]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String,System.String,System.String,System.DirectoryServices.AuthenticationTypes)", 3)]
    public static object Init(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && path.ToLower().StartsWith("ldap"))
            {
                IastModule.OnLdapInjection(path);
            }

            return path;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(DirectoryEntryAspect)}.{nameof(Init)}");
            return path;
        }
    }
}
