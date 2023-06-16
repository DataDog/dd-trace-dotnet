// <copyright file="GrpcProtocolHelpersBuildHttpErrorResponseIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NET461

#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Tagging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    /// <summary>
    /// Grpc.Net.Client.Internal.GrpcCall calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.AspNetCore.Server",
        TypeName = "Grpc.AspNetCore.Server.Internal.GrpcProtocolHelpers",
        MethodName = "BuildHttpErrorResponse",
        ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.HttpResponse", ClrNames.Int32, "Grpc.Core.StatusCode", ClrNames.String },
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GrpcProtocolHelpersBuildHttpErrorResponseIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="response">The HttpResponse value</param>
        /// <param name="httpStatusCode">The HTTP status code value to set</param>
        /// <param name="grpcStatusCode">The GRPC status code</param>
        /// <param name="message">The error message to set</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin(HttpResponse response, int httpStatusCode, int grpcStatusCode, string message)
        {
            var tracer = Tracer.Instance;
            if (GrpcCoreApiVersionHelper.IsSupported
             && tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc)
             && tracer.ActiveScope?.Span is Span { Tags: GrpcServerTags } span)
            {
                // This code path is only called when there's a fundamental failure that isn't even processed
                // (e.g. wrong Http protocol, invalid content-type etc)
                GrpcCommon.RecordFinalStatus(span, grpcStatusCode, message, ex: null);

                // There won't be any response metadata, as interceptors haven't executed, but we can grab
                // the request metadata directly from the HttpRequest
                var request = response.HttpContext.Request;
                span.SetHeaderTags(new HeadersCollectionAdapter(request.Headers), tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.RequestMetadataTagPrefix);
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
