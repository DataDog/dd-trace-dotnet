// <copyright file="MetadataArraySafeHandleCreateInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcLegacy.Client
{
    /// <summary>
    /// Grpc.Core.Internal calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Grpc.Core",
        TypeName = "Grpc.Core.Internal.MetadataArraySafeHandle",
        MethodName = "Create",
        ReturnTypeName = "Grpc.Core.Internal.MetadataArraySafeHandle",
        ParameterTypeNames = new[] { "Grpc.Core.Metadata" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(Grpc))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MetadataArraySafeHandleCreateInstrumentation
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        internal static CallTargetState OnMethodBegin<TInstance, TMetadata>(TInstance instance, TMetadata metadataInstance)
        {
            var tracer = Tracer.Instance;
            if (!GrpcCoreApiVersionHelper.IsSupported || !tracer.Settings.IsIntegrationEnabled(IntegrationId.Grpc) || metadataInstance is null)
            {
                return CallTargetState.GetDefault();
            }

            // This integration is called in both the server and client-side paths
            // and is responsible for serializing the metadata. However, we don't want it it serialize
            // our Temporary headers, so we remove them before the serialize, and restore them afterwards.
            // But we only add the extra headers for the client side code, so do a short-circuit check
            // for one of the temporary headers that should always be there in client side code
            var metadata = metadataInstance.DuckCast<IMetadata>();
            if (GetAndRemove(metadata, TemporaryHeaders.MethodKind) is { } methodKind)
            {
                // Remove our temporary headers so they don't get sent over the wire
                var service = GetAndRemove(metadata, TemporaryHeaders.Service);
                var methodName = GetAndRemove(metadata, TemporaryHeaders.MethodName);
                var startTime = GetAndRemove(metadata, TemporaryHeaders.StartTime);

                var parentId = GetAndRemove(metadata, TemporaryHeaders.ParentId);
                var parentService = GetAndRemove(metadata, TemporaryHeaders.ParentService);

                if (startTime is not null)
                {
                    var temporaryHeaders = new TemporaryGrpcHeaders(
                        metadata,
                        service: service,
                        methodKind: methodKind,
                        methodName: methodName,
                        startTime: startTime,
                        parentId: parentId,
                        parentService: parentService);

                    return new CallTargetState(scope: null, state: temporaryHeaders);
                }
            }

            return CallTargetState.GetDefault();

            static object? GetAndRemove<T>(T metadata, string header)
                where T : IMetadata
            {
                var headerValue = metadata.Get(header);
                if (headerValue is not null)
                {
                    metadata.Remove(headerValue);
                }

                return headerValue;
            }
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        internal static CallTargetReturn<TResponse> OnMethodEnd<TInstance, TResponse>(TInstance instance, TResponse response, Exception exception, in CallTargetState state)
        {
            if (state.State is TemporaryGrpcHeaders headers)
            {
                // Add our temporary headers back in, so we can access them later
                var metadata = headers.Metadata;
                metadata.Add(headers.MethodKind);
                if (headers.MethodName is not null)
                {
                    metadata.Add(headers.MethodName);
                }

                if (headers.Service is not null)
                {
                    metadata.Add(headers.Service);
                }

                metadata.Add(headers.StartTime);

                if (headers.ParentId is not null)
                {
                    metadata.Add(headers.ParentId);
                }

                if (headers.ParentService is not null)
                {
                    metadata.Add(headers.ParentService);
                }
            }

            return new CallTargetReturn<TResponse>(response);
        }

        private class TemporaryGrpcHeaders
        {
            public TemporaryGrpcHeaders(
                IMetadata metadata,
                object? service,
                object? methodName,
                object startTime,
                object methodKind,
                object? parentId,
                object? parentService)
            {
                Metadata = metadata;
                Service = service;
                MethodName = methodName;
                StartTime = startTime;
                MethodKind = methodKind;
                ParentId = parentId;
                ParentService = parentService;
            }

            public IMetadata Metadata { get; }

            public object? Service { get; }

            public object? MethodName { get; }

            public object StartTime { get; }

            public object MethodKind { get; }

            public object? ParentId { get; }

            public object? ParentService { get; }
        }
    }
}
