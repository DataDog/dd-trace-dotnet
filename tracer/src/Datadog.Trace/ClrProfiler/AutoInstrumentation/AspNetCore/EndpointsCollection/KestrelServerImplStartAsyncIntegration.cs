// <copyright file="KestrelServerImplStartAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP2_2_OR_GREATER

using System;
using System.ComponentModel;
using System.Threading;
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
    MinimumVersion = "2.2.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    IntegrationName = nameof(IntegrationId.AspNetCore))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class KestrelServerImplStartAsyncIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<KestrelServerImplStartAsyncIntegration>();

    internal static CallTargetState OnMethodBegin<TTarget, TApplication>(TTarget instance, ref TApplication? application, ref CancellationToken cancellationToken)
    {
        try
        {
            GatherEndpoints(instance);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Api Sec: Error gathering endpoints.");
        }

        return CallTargetState.GetDefault();
    }

    private static void GatherEndpoints(object? instance)
    {
        if (instance == null)
        {
            return;
        }

        if (!instance.TryDuckCast<IKestrelServer>(out var kestrelServer))
        {
            return;
        }

        if (!kestrelServer.Options.TryDuckCast<IKestrelServerOptions>(out var serviceProvider))
        {
            return;
        }

        var endpointDataSourceType = Type.GetType("Microsoft.AspNetCore.Routing.EndpointDataSource, Microsoft.AspNetCore.Routing");
        if (endpointDataSourceType == null)
        {
            return;
        }

        if (!serviceProvider.ApplicationServices.GetService(endpointDataSourceType).TryDuckCast<IEndpointDataSource>(out var endpointDataSource))
        {
            return;
        }

        AppSec.EndpointsCollection.CollectEndpoints(endpointDataSource.Endpoints);
    }
}

#endif
