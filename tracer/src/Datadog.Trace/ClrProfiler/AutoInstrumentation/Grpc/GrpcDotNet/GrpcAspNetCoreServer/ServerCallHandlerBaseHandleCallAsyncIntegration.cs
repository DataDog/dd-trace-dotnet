// <copyright file="ServerCallHandlerBaseHandleCallAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NET461
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    /// <summary>
    /// Grpc.Net.Client.Internal.GrpcCall calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.AspNetCore.Server",
        TypeName = "Grpc.AspNetCore.Server.Internal.CallHandlers.ServerCallHandlerBase`3",
        MethodName = "HandleCallAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpContext" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ServerCallHandlerBaseHandleCallAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="httpContext">HttpContext instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, HttpContext httpContext)
        {
            if (GrpcCoreApiVersionHelper.IsSupported)
            {
                var scope = GrpcDotNetServerCommon.CreateServerSpan(Tracer.Instance, instance, httpContext.Request);
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            // There would only be an exception here if something was wrong in the infrastructure,
            // which should basically never happen, but playing it safe
            // Note that if this _did_ happen it would overwrite any exception from the handler etc
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
#endif
