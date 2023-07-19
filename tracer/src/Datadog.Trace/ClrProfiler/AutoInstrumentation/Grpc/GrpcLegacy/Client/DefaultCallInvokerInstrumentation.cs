// <copyright file="DefaultCallInvokerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    /// <summary>
    /// Grpc.Core.Internal calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Core",
        TypeName = "Grpc.Core.DefaultCallInvoker",
        MethodName = "CreateCall",
        ReturnTypeName = "Grpc.Core.CallInvocationDetails`2[!!0,!!1]",
        ParameterTypeNames = new[] { "Grpc.Core.Method`2[!!0,!!1]", ClrNames.String, "Grpc.Core.CallOptions" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DefaultCallInvokerInstrumentation
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMethod">Type of the Method{Request, Response}</typeparam>
        /// <typeparam name="TCallOptions">Type of the CallOptions</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="method">The Method{Request, Response} instance</param>
        /// <param name="host">The host name</param>
        /// <param name="callOptions">The CallOptions instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMethod, TCallOptions>(TTarget instance, TMethod method, string host, ref TCallOptions callOptions)
            where TMethod : IMethod
        {
            // Can't use a constraint for TMethod as need the _original_ type
            // Create a dummy span, and inject the propagation headers
            // Need to inject _everything_ required to recreate the span later, in the "finished" integrations
            if (GrpcCoreApiVersionHelper.IsSupported)
            {
                GrpcLegacyClientCommon.InjectHeaders(Tracer.Instance, method, ref callOptions);
            }

            return CallTargetState.GetDefault();
        }
    }
}
