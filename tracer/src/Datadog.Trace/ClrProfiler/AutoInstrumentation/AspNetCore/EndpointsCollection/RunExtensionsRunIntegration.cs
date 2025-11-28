// <copyright file="RunExtensionsRunIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if !NETFRAMEWORK

using System.ComponentModel;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.EndpointsCollection;

/// <summary>
/// System.Void Microsoft.AspNetCore.Builder.RunExtensions::Run(Microsoft.AspNetCore.Builder.IApplicationBuilder,Microsoft.AspNetCore.Http.RequestDelegate) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Http.Abstractions",
    TypeName = "Microsoft.AspNetCore.Builder.RunExtensions",
    MethodName = "Run",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["Microsoft.AspNetCore.Builder.IApplicationBuilder", "Microsoft.AspNetCore.Http.RequestDelegate"],
    MinimumVersion = "2.2.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RunExtensionsRunIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TApp, THandler>(ref TApp? app, ref THandler? handler)
    {
        if (Security.Instance.ApiSecurity.CanCollectEndpoints())
        {
            MapEndpointsCollection.DetectedAvailableEndpoint();
        }

        return CallTargetState.GetDefault();
    }
}

#endif
