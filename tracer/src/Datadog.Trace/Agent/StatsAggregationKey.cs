// <copyright file="StatsAggregationKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        public readonly string SpanKind;
        public readonly int IsTraceRoot; // 0=NotSet, 1=True, 2=False
        public readonly string PeerTagsHash;
        public readonly string HttpMethod;
        public readonly string HttpEndpoint;
        public readonly string GrpcStatusCode;

        public StatsAggregationKey(
            string resource,
            string service,
            string operationName,
            string type,
            int httpStatusCode,
            bool isSyntheticsRequest,
            string spanKind,
            int isTraceRoot,
            string peerTagsHash,
            string httpMethod,
            string httpEndpoint,
            string grpcStatusCode)
        {
            Resource = resource;
            Service = service;
            OperationName = operationName;
            Type = type;
            HttpStatusCode = httpStatusCode;
            IsSyntheticsRequest = isSyntheticsRequest;
            SpanKind = spanKind;
            IsTraceRoot = isTraceRoot;
            PeerTagsHash = peerTagsHash;
            HttpMethod = httpMethod;
            HttpEndpoint = httpEndpoint;
            GrpcStatusCode = grpcStatusCode;
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
                && SpanKind == other.SpanKind
                && IsTraceRoot == other.IsTraceRoot
                && PeerTagsHash == other.PeerTagsHash
                && HttpMethod == other.HttpMethod
                && HttpEndpoint == other.HttpEndpoint
                && GrpcStatusCode == other.GrpcStatusCode;
        }

        public override bool Equals(object obj)
        {
            return obj is StatsAggregationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Resource is not null ? Resource.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Service is not null ? Service.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OperationName is not null ? OperationName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Type is not null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ HttpStatusCode;
                hashCode = (hashCode * 397) ^ IsSyntheticsRequest.GetHashCode();
                hashCode = (hashCode * 397) ^ (SpanKind is not null ? SpanKind.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsTraceRoot;
                hashCode = (hashCode * 397) ^ (PeerTagsHash is not null ? PeerTagsHash.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (HttpMethod is not null ? HttpMethod.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (HttpEndpoint is not null ? HttpEndpoint.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (GrpcStatusCode is not null ? GrpcStatusCode.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
