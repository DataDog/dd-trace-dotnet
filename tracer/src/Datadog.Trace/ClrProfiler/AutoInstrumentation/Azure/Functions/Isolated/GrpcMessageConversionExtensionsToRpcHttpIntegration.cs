// <copyright file="GrpcMessageConversionExtensionsToRpcHttpIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
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
    {
        return new CallTargetState(scope: null, state: capabilities);
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget nullInstance, TReturn returnValue, Exception? exception, in CallTargetState state)
        where TReturn : ITypedData, IDuckType
    {
        var capabilities = state.State.DuckCast<IGrpcCapabilities>();
        if (capabilities is null)
        {
            // Something went wrong, this shouldn't happen
            return returnValue;
        }

        // The HTTP request represented by TypedData is essentially a duplicate of the original incoming
        // request that was received by func.exe. This is used to create a span representing
        // the request from the client. The typed data is then sent by the GRPC connection
        // to the functions app and is used to invoke the actual function. We intercept that
        // in the functions app and use it to create a span representing the actual work of the app.
        // In order for the span hierarchy/parenting to work correctly, we need to replace the parentID
        // in the GRPC http request representation, which is what we're doing here by overwriting all
        // the existing datadog headers
        //
        // However, when using the AspNetCore integration, things work a bit differently. The
        // func.exe app instead primarily _proxies_ the HTTP request (if it is an HTTP trigger) to the functions
        // app, instead of sending the bulk of the context as a gRPC message. This means that we _shouldn't_ inject the
        // context into the gRPC request, because the context is already present in the incoming HTTP request, and is
        // used preferentially.
        //
        // What's more, in the case of HTTP triggers with proxying enabled, the TypedData returnValue returned from
        // this is method is a shared object, so mutating it can cause issues with other parts of the system.
        // See https://github.com/Azure/azure-functions-host/blob/420a4686802612857cae35cefea2b685283507c9/src/WebJobs.Script.Grpc/MessageExtensions/GrpcMessageConversionExtensions.cs#L104-L126

        var isHttpProxying = !string.IsNullOrEmpty(capabilities.GetCapabilityState("HttpUri"));
        if (isHttpProxying)
        {
            // The returnValue might not be the singleton instance, but as the HttpRequest is going
            // to be forwarded anyway with the correct headers, we don't bother injecting here.
            // TODO: is that _actually_ correct? Do we actually need to create a new TypedInfo object and inject that?
            return returnValue;
        }

        // If we're not proxying, we can safely inject the context into the gRPC request
        var useNullableHeaders = !string.IsNullOrEmpty(capabilities.GetCapabilityState("UseNullableValueDictionaryForHttp"));
        AzureFunctionsCommon.OverridePropagatedContext<TTarget, TReturn>(Tracer.Instance, returnValue, useNullableHeaders);

        return returnValue;
    }
}
#endif
