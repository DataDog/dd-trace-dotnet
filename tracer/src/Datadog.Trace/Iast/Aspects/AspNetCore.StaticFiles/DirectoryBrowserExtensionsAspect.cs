// <copyright file="DirectoryBrowserExtensionsAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.AspNetCore.StaticFiles;

/// <summary> DirectoryBrowserExtensions class aspect </summary>
[AspectClass("Microsoft.AspNetCore.StaticFiles", AspectType.Sink, VulnerabilityType.DirectoryListingLeak)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DirectoryBrowserExtensionsAspect
{
    /// <summary>
    /// UseDirectoryBrowser aspects
    /// </summary>
    /// <param name="obj">Object</param>
    /// <returns> The object </returns>
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions::UseDirectoryBrowser(Microsoft.AspNetCore.Builder.IApplicationBuilder)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions::UseDirectoryBrowser(Microsoft.AspNetCore.Builder.IApplicationBuilder,Microsoft.AspNetCore.Builder.DirectoryBrowserOptions)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions::UseDirectoryBrowser(Microsoft.AspNetCore.Builder.IApplicationBuilder,System.String)")]
    public static object UseDirectoryBrowser(object obj)
    {
        IastModule.OnDirectoryListingLeak();
        return obj;
    }
}
