// <copyright file="HttpContextServerCallContextLogCallEndIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NET461

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    /// <summary>
    /// Grpc.Net.Client.Internal.GrpcCall calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.AspNetCore.Server",
        TypeName = "Grpc.AspNetCore.Server.Internal.HttpContextServerCallContext",
        MethodName = "LogCallEnd",
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HttpContextServerCallContextLogCallEndIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            var tracer = Tracer.Instance;
            if (GrpcCoreApiVersionHelper.IsSupported
             && tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc)
             && tracer.ActiveScope?.Span is Span { Tags: GrpcServerTags } span)
            {
                var callContext = instance.DuckCast<HttpContextServerCallContextStruct>();
                var status = callContext.StatusCore;
                GrpcCommon.RecordFinalStatus(span, status.StatusCode, status.Detail, status.DebugException);

                if (callContext.RequestHeaders is { Count: > 0 } requestMetadata)
                {
                    span.SetHeaderTags(new MetadataHeadersCollection(requestMetadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.RequestMetadataTagPrefix);
                }

                if (callContext.ResponseTrailers is { Count: > 0 } responseMetadata)
                {
                    span.SetHeaderTags(new MetadataHeadersCollection(responseMetadata), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
