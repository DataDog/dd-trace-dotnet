// <copyright file="GrpcDotNetClientCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    internal static class GrpcDotNetClientCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GrpcDotNetClientCommon));

        public static Scope? CreateClientSpan<TGrpcCall, TRequest>(Tracer tracer, TGrpcCall instance, TRequest requestMessage)
            where TRequest : IHttpRequestMessage
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc) || instance is null)
            {
                return null;
            }

            Scope? scope = null;

            // Can't use constraints for this one, as we're in a generic class
            var grpcCall = instance.DuckCast<IGrpcCall>();

            try
            {
                var clientSchema = tracer.CurrentTraceSettings.Schema.Client;
                var tags = clientSchema.CreateGrpcClientTags();
                var method = grpcCall.Method;
                tags.Host = HttpRequestUtils.GetNormalizedHost(grpcCall.Channel.Address.Host);
                GrpcCommon.AddGrpcTags(tags, tracer, method.GrpcType, name: method.Name, path: method.FullName, serviceName: method.ServiceName);

                var operationName = clientSchema.GetOperationNameForProtocol("grpc");
                var serviceName = clientSchema.GetServiceName(component: "grpc-client");
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, startTime: null);

                var span = scope.Span;
                span.Type = SpanTypes.Grpc;
                span.ResourceName = method.FullName;

                // add distributed tracing headers to the HTTP request
                // These will be overwritten by the HttpClient integration if that is enabled, per the RFC
                SpanContextPropagator.Instance.Inject(span.Context, new HttpHeadersCollection(requestMessage.Headers));

                // Add the request metadata as tags
                if (grpcCall.Options.Headers is { Count: > 0 })
                {
                    var metadata = new MetadataHeadersCollection(grpcCall.Options.Headers);
                    span.SetHeaderTags(metadata, tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.RequestMetadataTagPrefix);
                }

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.Grpc);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating client span for GRPC call");
            }

            return scope;
        }

        public static void RecordResponseMetadataAndStatus<TGrpcCall>(Tracer tracer, TGrpcCall instance, int grpcStatusCode, string errorMessage, Exception? ex)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc)
             || instance is null
             || tracer.ActiveScope?.Span is not Span { Tags: GrpcClientTags } span)
            {
                return;
            }

            var grpcCall = instance.DuckCast<IGrpcCall>();

            // The response headers may include metadata
            if (grpcCall.HttpResponse is { Headers: { } responseHeaders })
            {
                var metadata = new HttpResponseHeadersCollection(responseHeaders);
                span.SetHeaderTags(metadata, tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
            }

            // Note that this will be null if they haven't read the response body yet, but we can't force that on users
            if (grpcCall.TryGetTrailers(out var trailers) && trailers is { Count: > 0 })
            {
                var metadata = new MetadataHeadersCollection(trailers);
                span.SetHeaderTags(metadata, tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
            }
            else if (grpcCall.HttpResponse is { } httpResponse
                  && httpResponse.DuckCast<HttpResponseStruct>().TrailingHeaders is { } trailingHeaders)
            {
                var metadata = new HttpResponseHeadersCollection(trailingHeaders);
                span.SetHeaderTags(metadata, tracer.Settings.GrpcTagsInternal, defaultTagPrefix: GrpcCommon.ResponseMetadataTagPrefix);
            }

            GrpcCommon.RecordFinalClientSpanStatus(Tracer.Instance, grpcStatusCode, errorMessage, ex);
        }
    }
}
#endif
