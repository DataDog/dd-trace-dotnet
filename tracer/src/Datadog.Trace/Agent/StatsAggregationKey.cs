// <copyright file="StatsAggregationKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent
{
    internal readonly struct StatsAggregationKey : IEquatable<StatsAggregationKey>
    {
        public readonly string Resource;
        public readonly string Service;
        public readonly string OperationName;
        public readonly string Type;
        public readonly int HttpStatusCode;
        public readonly bool IsSyntheticsRequest;
        public readonly bool IsError;
        public readonly bool IsTopLevel;
        public readonly string SpanKind;
        public readonly bool IsTraceRoot;
        public readonly string HttpMethod;
        public readonly string HttpEndpoint;
        public readonly string GrpcStatusCode;
        public readonly string ServiceSource;
        public readonly ulong PeerTagsHash;

        // Constructs a StatsAgregationKey that represents the aggregation key used by Datadog,
        // which does not include IsError and IsTopLevel, since these should be part of the same timeseries
        public StatsAggregationKey(
            string resource,
            string service,
            string operationName,
            string type,
            int httpStatusCode,
            bool isSyntheticsRequest,
            string spanKind,
            bool isError,
            bool isTopLevel,
            bool isTraceRoot,
            string httpMethod,
            string httpEndpoint,
            string grpcStatusCode,
            string serviceSource,
            ulong peerTagsHash)
        {
            Resource = resource;
            Service = service;
            OperationName = operationName;
            Type = type;
            HttpStatusCode = httpStatusCode;
            IsSyntheticsRequest = isSyntheticsRequest;
            IsError = isError;
            IsTopLevel = isTopLevel;
            SpanKind = spanKind;
            IsTraceRoot = isTraceRoot;
            HttpMethod = httpMethod;
            HttpEndpoint = httpEndpoint;
            GrpcStatusCode = grpcStatusCode;
            ServiceSource = serviceSource;
            PeerTagsHash = peerTagsHash;
        }

        public bool Equals(StatsAggregationKey other)
        {
            return
                Resource == other.Resource
                && Service == other.Service
                && OperationName == other.OperationName
                && Type == other.Type
                && HttpStatusCode == other.HttpStatusCode
                && IsSyntheticsRequest == other.IsSyntheticsRequest
                && IsError == other.IsError
                && IsTopLevel == other.IsTopLevel
                && IsSyntheticsRequest == other.IsSyntheticsRequest
                && SpanKind == other.SpanKind
                && IsTraceRoot == other.IsTraceRoot
                && HttpMethod == other.HttpMethod
                && HttpEndpoint == other.HttpEndpoint
                && GrpcStatusCode == other.GrpcStatusCode
                && ServiceSource == other.ServiceSource
                && PeerTagsHash == other.PeerTagsHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is StatsAggregationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Resource);
            hashCode.Add(Service);
            hashCode.Add(OperationName);
            hashCode.Add(Type);
            hashCode.Add(HttpStatusCode);
            hashCode.Add(IsSyntheticsRequest);
            hashCode.Add(IsError);
            hashCode.Add(IsTopLevel);
            hashCode.Add(SpanKind);
            hashCode.Add(IsTraceRoot);
            hashCode.Add(HttpMethod);
            hashCode.Add(HttpEndpoint);
            hashCode.Add(GrpcStatusCode);
            hashCode.Add(ServiceSource);
            hashCode.Add(PeerTagsHash);
            return hashCode.ToHashCode();
        }
    }
}
