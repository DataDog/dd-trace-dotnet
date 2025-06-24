// <copyright file="MapExtensionsMapIntegrationV5Plus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.EndpointsCollection;

/// <summary>
/// Microsoft.AspNetCore.Builder.IApplicationBuilder Microsoft.AspNetCore.Builder.MapExtensions::Map(Microsoft.AspNetCore.Builder.IApplicationBuilder,Microsoft.AspNetCore.Http.PathString,System.Boolean,System.Action`1[Microsoft.AspNetCore.Builder.IApplicationBuilder]) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Http.Abstractions",
    TypeName = "Microsoft.AspNetCore.Builder.MapExtensions",
    MethodName = "Map",
    ReturnTypeName = "Microsoft.AspNetCore.Builder.IApplicationBuilder",
    ParameterTypeNames = ["Microsoft.AspNetCore.Builder.IApplicationBuilder", "Microsoft.AspNetCore.Http.PathString", ClrNames.Bool, "System.Action`1[Microsoft.AspNetCore.Builder.IApplicationBuilder]"],
    MinimumVersion = "5.0.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class MapExtensionsMapIntegrationV5Plus
{
    internal static CallTargetState OnMethodBegin<TTarget, TApp, TPathMatch, TConfiguration>(ref TApp? app, ref TPathMatch pathMatch, ref bool preserveMatchedPathSegment, ref TConfiguration? configuration)
    {
        if (Security.Instance.ApiSecurity.CanCollectEndpoints())
        {
            MapEndpointsCollection.BeggingMapEndpoint(pathMatch!.ToString()!);
        }

        return CallTargetState.GetDefault();
    }

    internal static CallTargetReturn<TReturn?> OnMethodEnd<TTarget, TReturn>(TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (Security.Instance.ApiSecurity.CanCollectEndpoints())
        {
            MapEndpointsCollection.EndMapEndpoint();
        }

        return new CallTargetReturn<TReturn?>(returnValue);
    }
}

#endif
