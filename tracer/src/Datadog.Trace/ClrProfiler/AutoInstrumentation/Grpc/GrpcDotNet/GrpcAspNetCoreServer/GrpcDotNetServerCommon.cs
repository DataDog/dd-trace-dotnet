// <copyright file="GrpcDotNetServerCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NET461

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer
{
    internal static class GrpcDotNetServerCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GrpcDotNetServerCommon));

        public static Scope? CreateServerSpan<T>(Tracer tracer, T instance, HttpRequest requestMessage)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc))
            {
                return null;
            }

            Scope? scope = null;
            var method = instance.DuckCast<ServerCallHandlerBaseStruct>().MethodInvoker.Method;
            try
            {
                var tags = new GrpcServerTags();
                GrpcCommon.AddGrpcTags(tags, tracer, method.GrpcType, name: method.Name, path: method.FullName, serviceName: method.ServiceName);

                // If we have a local span (e.g. from aspnetcore) then use that as the parent
                // Otherwise, use the distributed context as the parent
                var spanContext = tracer.ActiveScope?.Span.Context;
                if (spanContext is null)
                {
                    spanContext = ExtractPropagatedContext(requestMessage);
                }

                var serviceName = tracer.DefaultServiceName ?? "grpc-server";
                string operationName = tracer.CurrentTraceSettings.Schema.Server.GetOperationNameForProtocol("grpc");
                scope = tracer.StartActiveInternal(operationName, parent: spanContext, tags: tags, serviceName: serviceName);

                var span = scope.Span;
                span.Type = SpanTypes.Grpc;
                span.ResourceName = method.FullName;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.Grpc);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating server span for GRPC call");
            }

            return scope;
        }

        private static SpanContext? ExtractPropagatedContext(HttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(new HeadersCollectionAdapter(requestHeaders));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }
    }
}
#endif
