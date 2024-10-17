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

        public static bool IsASupportedVersion<TTarget>()
        {
            var isSupported = SupportedVersionByTypeCache<TTarget>.IsSupported;
            if (SupportedVersionByTypeCache<TTarget>.Version is { } version)
            {
                Log.Debug("Version {AssemblyVersion} of {Assembly} from type {Type} is {Supported}.", version, typeof(TTarget).Assembly.FullName, typeof(TTarget).FullName, isSupported ? "supported" : "not supported");
            }
            else
            {
                Log.Debug("Error fetching assembly version of {Assembly} from type {Type}, not supported", typeof(TTarget).Assembly.FullName, typeof(TTarget).FullName);
            }

            return isSupported;
        }

        public static Scope? CreateServerSpan<T>(Tracer tracer, T instance, HttpRequest requestMessage)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc))
            {
                return null;
            }

            // Check if the current handler has the MethodInvoker property
            MethodStruct method;
            if (instance.TryDuckCast<ServerCallHandlerBaseStruct>(out var handlerWithMethodInvoker))
            {
                // The current handler has the MethodInvoker property (this is the case for >= 2.27.0 versions)
                method = handlerWithMethodInvoker.MethodInvoker.Method;
            }
            else
            {
                return null;
            }

            Scope? scope = null;
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

        private static class SupportedVersionByTypeCache<TTarget>
        {
            static SupportedVersionByTypeCache()
            {
                // The assembly version of Grpc.AspNetCore.Server is fixed to 2.0.0, so we need to check the FileVersion
                // to know if we should instrument this library.
                if (typeof(TTarget).TryGetAssemblyFileVersionFromType(out var version))
                {
                    Version = version;
                    // Grpc.AspNetCore.Server 2.27.0 is the minimum version supported by this implementation.
                    IsSupported = version >= new Version(2, 27, 0);
                }
                else
                {
                    IsSupported = false;
                }
            }

            // ReSharper disable once StaticMemberInGenericType
            public static bool IsSupported { get; }

            public static Version? Version { get; }
        }
    }
}
#endif
