// <copyright file="KestrelServerImplStartAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.EndpointsCollection;

/// <summary>
/// System.Threading.Tasks.Task Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl::StartAsync[TContext](Microsoft.AspNetCore.Hosting.Server.IHttpApplication`1[TContext],System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.Kestrel.Core",
    TypeName = "Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl",
    MethodName = "StartAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["Microsoft.AspNetCore.Hosting.Server.IHttpApplication`1[!!0]", ClrNames.CancellationToken],
    MinimumVersion = "5.0.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[InstrumentMethod(
    AssemblyName = "Microsoft.AspNetCore.Server.Kestrel.Core",
    TypeName = "Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServer",
    MethodName = "StartAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["Microsoft.AspNetCore.Hosting.Server.IHttpApplication`1[!!0]", ClrNames.CancellationToken],
    MinimumVersion = "2.2.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class KestrelServerImplStartAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(KestrelServerImplStartAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget, TApplication>(TTarget instance, ref TApplication? application, ref CancellationToken cancellationToken)
        where TTarget : IKestrelServer
    {
        try
        {
            if (Security.Instance.ApiSecurity.CanCollectEndpoints())
            {
                GatherEndpoints(instance);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "API Security: Failed to collect endpoints.");
        }

        return CallTargetState.GetDefault();
    }

    private static void GatherEndpoints(IKestrelServer kestrelServer)
    {
        if (!kestrelServer.Options.TryDuckCast<IKestrelServerOptions>(out var serviceProvider))
        {
            Log.Warning("API Security: Endpoints collection: Failed to duck the Server Options.");
            return;
        }

        var endpointDataSourceType = Type.GetType("Microsoft.AspNetCore.Routing.EndpointDataSource, Microsoft.AspNetCore.Routing");
        if (endpointDataSourceType == null)
        {
            Log.Warning("API Security: Endpoints collection: Failed to get the EndpointDataSource Type.");
            return;
        }

        if (!serviceProvider.ApplicationServices.GetService(endpointDataSourceType).TryDuckCast<IEndpointDataSource>(out var endpointDataSource))
        {
            Log.Warning("API Security: Endpoints collection: Failed to get/duck the EndpointDataSource Service.");
            return;
        }

        AppSec.EndpointsCollection.CollectEndpoints(endpointDataSource.Endpoints);
    }
}

#endif
