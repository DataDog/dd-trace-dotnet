// <copyright file="GrpcMessageConversionExtensionsToRpcHttpIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions;

/// <summary>
/// Azure Function calltarget instrumentation for
/// https://github.com/Azure/azure-functions-host/blob/8ceb05a89a4337f07264d4991545538a3e8b58a0/src/WebJobs.Script.Grpc/MessageExtensions/GrpcMessageConversionExtensions.cs#L103
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Azure.WebJobs.Script.Grpc",
    TypeName = "Microsoft.Azure.WebJobs.Script.Grpc.GrpcMessageConversionExtensions",
    MethodName = "ToRpcHttp",
    ReturnTypeName = "System.Threading.Tasks.Task`1[Microsoft.Azure.WebJobs.Script.Grpc.Messages.TypedData]",
    ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpRequest", "Microsoft.Extensions.Logging.ILogger", "Microsoft.Azure.WebJobs.Script.Grpc.GrpcCapabilities" },
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = AzureFunctionsCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class GrpcMessageConversionExtensionsToRpcHttpIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TLogger, TGrpcCapabilities>(TTarget nullInstance, HttpRequest request, TLogger logger, TGrpcCapabilities capabilities)
        where TGrpcCapabilities : IGrpcCapabilities
    {
        var capability = capabilities.GetCapabilityState("UseNullableValueDictionaryForHttp");
        return new CallTargetState(scope: null, state: capability);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget nullInstance, TReturn returnValue, Exception exception, in CallTargetState state)
        where TReturn : ITypedData
    {
        AzureFunctionsCommon.OverridePropagatedContext<TTarget, TReturn>(Tracer.Instance, returnValue, state.State as string);
        return returnValue;
    }
}
#endif
