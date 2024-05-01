// <copyright file="GrpcLegacyClientCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    internal static class GrpcLegacyClientCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GrpcLegacyClientCommon));
        private static readonly ConditionalWeakTable<object, string> ChannelToHostMap = new();

        public static Scope? CreateClientSpan(
            Tracer tracer,
            in CallInvocationDetailsStruct callInvocationDetails,
            in StatusStruct receivedStatus)
        {
            var requestMetadata = callInvocationDetails.Options.Headers;

            if (requestMetadata is null)
            {
                // We're in an undefined state, and can't reliably create the spans so bail out
                Log.Error("Unable to create GRPC client spans - RequestMetadata object was null");
                return null;
            }

            Scope? scope = null;
            try
            {
                // try extracting all the details we need
                var requestMetadataWrapper = new MetadataHeadersCollection(requestMetadata);
                var existingSpanContext = SpanContextPropagator.Instance.Extract(requestMetadataWrapper);

                // If this operation creates the trace, then we will need to re-apply the sampling priority
                bool setSamplingPriority = existingSpanContext?.SamplingPriority != null && tracer.ActiveScope == null;

                // grab the temporary values we stored in the metadata
                ExtractTemporaryHeaders(
                    requestMetadata,
                    existingSpanContext?.TraceId128 ?? default,
                    out var methodKind,
                    out var methodName,
                    out var grpcService,
                    out var startTime,
                    out var parentContext);

                var clientSchema = tracer.CurrentTraceSettings.Schema.Client;
                var operationName = clientSchema.GetOperationNameForProtocol("grpc");
                var serviceName = clientSchema.GetServiceName(component: "grpc-client");
                var tags = clientSchema.CreateGrpcClientTags();
                var methodFullName = callInvocationDetails.Method;

                tags.Host = GetNormalizedHost(callInvocationDetails.Channel.Instance!, callInvocationDetails.Channel.Target);
                GrpcCommon.AddGrpcTags(tags, tracer, methodKind, name: methodName, path: methodFullName, serviceName: grpcService);

                var span = tracer.StartSpan(
                    operationName,
                    parent: parentContext,
                    tags: tags,
                    spanId: existingSpanContext?.SpanId ?? 0,
                    traceId: existingSpanContext?.TraceId128 ?? default,
                    serviceName: serviceName,
                    startTime: startTime);

                span.Type = SpanTypes.Grpc;
                span.ResourceName = methodFullName;

                span.SetHeaderTags(requestMetadataWrapper, tracer.Settings.GrpcTagsInternal, GrpcCommon.RequestMetadataTagPrefix);
                scope = tracer.ActivateSpan(span);

                if (setSamplingPriority && existingSpanContext?.SamplingPriority is { } samplingPriority)
                {
                    span.Context.TraceContext?.SetSamplingPriority(samplingPriority);
                }

                GrpcCommon.RecordFinalStatus(span, receivedStatus.StatusCode, receivedStatus.Detail, receivedStatus.DebugException);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.Grpc);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating client span for GRPC call");
            }

            return scope;
        }

        public static void InjectHeaders<TMethod, TCallOptions>(Tracer tracer, TMethod method, ref TCallOptions callOptionsInstance)
            where TMethod : IMethod
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc) || callOptionsInstance is null)
            {
                return;
            }

            try
            {
                var span = CreateInactiveSpan(tracer, method.FullName);

                IMetadata proxy;
                var callOptions = callOptionsInstance.DuckCast<ICallOptions>();
                if (callOptions.Headers?.IsReadOnly == true)
                {
                    proxy = callOptions.Headers;
                }
                else
                {
                    var metadata = CachedMetadataHelper<TCallOptions>.CreateMetadata();
                    proxy = metadata.DuckCast<IMetadata>();

                    if (callOptions.Headers is { Count: > 0 })
                    {
                        // copy everything from the old one into the new one
                        foreach (var entry in callOptions.Headers)
                        {
                            proxy.Add(entry!);
                        }
                    }

                    // Replace the existing callOptions with a new instance (with the headers set)
                    callOptionsInstance = (TCallOptions)callOptions.WithHeaders(metadata);
                }

                // Save _everything_ we need in the metadata (we'll remove the cruft before serializing it)
                var collection = new MetadataHeadersCollection(proxy);

                // Add the headers that we need to recreate the span later
                AddTemporaryHeaders(
                    proxy,
                    grpcType: method.GrpcType,
                    methodName: method.Name,
                    serviceName: method.ServiceName,
                    startTime: span.StartTime,
                    parentContext: span.Context.ParentInternal);

                // Add the propagation headers
                SpanContextPropagator.Instance.Inject(span.Context, collection);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating inactive client span for GRPC call");
            }
        }

        internal static string GetNormalizedHost(object channelInstance, string target)
        {
            if (ChannelToHostMap.TryGetValue(channelInstance, out var normalizedHost))
            {
                return normalizedHost;
            }

            var host = target.IndexOf(':') switch
            {
                -1 => target,
                var index => target.Substring(startIndex: 0, length: index),
            };

            normalizedHost = HttpRequestUtils.GetNormalizedHost(host);

#if NETCOREAPP3_1_OR_GREATER
            ChannelToHostMap.AddOrUpdate(channelInstance, normalizedHost);
#else
            ChannelToHostMap.GetValue(channelInstance, x => normalizedHost);
#endif

            return normalizedHost;
        }

        private static void AddTemporaryHeaders(IMetadata metadata, int grpcType, string? methodName, string? serviceName, DateTimeOffset startTime, ISpanContext? parentContext)
        {
            metadata.Add(TemporaryHeaders.MethodKind, GrpcCommon.GetGrpcMethodKind(grpcType));
            if (methodName is not null)
            {
                metadata.Add(TemporaryHeaders.MethodName, methodName);
            }

            if (serviceName is not null)
            {
                metadata.Add(TemporaryHeaders.Service, serviceName);
            }

            metadata.Add(TemporaryHeaders.StartTime, startTime.ToUnixTimeMilliseconds().ToString());
            if (parentContext is not null)
            {
                metadata.Add(TemporaryHeaders.ParentId, parentContext.SpanId.ToString());
                var parentService = parentContext is SpanContext s ? s.ServiceNameInternal : parentContext.ServiceName;
                if (parentService is not null)
                {
                    metadata.Add(TemporaryHeaders.ParentService, parentService);
                }
            }
        }

        private static void ExtractTemporaryHeaders(
            IMetadata metadata,
            TraceId traceId,
            out string methodKind,
            out string? methodName,
            out string? serviceName,
            out DateTimeOffset startTime,
            out ISpanContext? parentContext)
        {
            // These should always be available
            var deserializedMethodKind = metadata.Get(TemporaryHeaders.MethodKind)?.DuckCast<MetadataEntryStruct>().Value;
            if (deserializedMethodKind is null)
            {
                // Shouldn't ever happen, but play it safe
                Log.Warning("Temporary GRPC header x-datadog-temp-kind not found - assuming Unary request");
            }

            methodKind = deserializedMethodKind ?? GrpcMethodKinds.Unary;
            methodName = metadata.Get(TemporaryHeaders.MethodName)?.DuckCast<MetadataEntryStruct>().Value;
            serviceName = metadata.Get(TemporaryHeaders.Service)?.DuckCast<MetadataEntryStruct>().Value;
            var unixTimeMilliseconds = metadata.Get(TemporaryHeaders.StartTime)?.DuckCast<MetadataEntryStruct>().Value;
            startTime = long.TryParse(unixTimeMilliseconds, out var milliseconds)
                ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                : DateTimeOffset.UtcNow;

            parentContext = null;

            if (traceId != TraceId.Zero)
            {
                var parentIdString = metadata.Get(TemporaryHeaders.ParentId)?.DuckCast<MetadataEntryStruct>().Value;
                var parentService = metadata.Get(TemporaryHeaders.ParentService)?.DuckCast<MetadataEntryStruct>().Value;
                if (!string.IsNullOrEmpty(parentService) && ulong.TryParse(parentIdString, out var parentId))
                {
                    parentContext = new ReadOnlySpanContext(traceId, parentId, parentService);
                }
            }
        }

        private static Span CreateInactiveSpan(Tracer tracer, string? methodFullName)
        {
            var operationName = tracer.CurrentTraceSettings.Schema.Client.GetOperationNameForProtocol("grpc");
            var serviceName = tracer.CurrentTraceSettings.Schema.Client.GetServiceName(component: "grpc-client");
            var tags = tracer.CurrentTraceSettings.Schema.Client.CreateGrpcClientTags();
            tags.SetAnalyticsSampleRate(IntegrationId.Grpc, tracer.Settings, enabledWithGlobalSetting: false);
            tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

            var span = tracer.StartSpan(operationName, tags, serviceName: serviceName, addToTraceContext: false);
            span.Type = SpanTypes.Grpc;
            span.ResourceName = methodFullName;

            return span;
        }
    }
}
