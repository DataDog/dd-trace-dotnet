// <copyright file="DirectoryBrowserExtensionsUseDirectoryBrowserIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.StaticFiles;

/// <summary>
/// Microsoft.AspNetCore.Builder.IApplicationBuilder Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions::UseDirectoryBrowser(Microsoft.AspNetCore.Builder.IApplicationBuilder) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.StaticFiles",
    TypeName = "Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions",
    MethodName = MethodName,
    ReturnTypeName = "Microsoft.AspNetCore.Builder.IApplicationBuilder",
    ParameterTypeNames = ["Microsoft.AspNetCore.Builder.IApplicationBuilder"],
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.StaticFiles",
    TypeName = "Microsoft.AspNetCore.Builder.DirectoryBrowserExtensions",
    MethodName = MethodName,
    ReturnTypeName = "Microsoft.AspNetCore.Builder.IApplicationBuilder",
    ParameterTypeNames = ["Microsoft.AspNetCore.Builder.IApplicationBuilder", "Microsoft.AspNetCore.Builder.DirectoryBrowserOptions"],
    MinimumVersion = "2",
    MaximumVersion = "8",
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DirectoryBrowserExtensionsUseDirectoryBrowserIntegration
{
    private const string MethodName = "UseDirectoryBrowser";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DirectoryBrowserExtensionsUseDirectoryBrowserIntegration>();

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        try
        {
            IastModule.OnDirectoryListingLeak(MethodName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to report Directory Listing Leak vulnerability");
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}
