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

        public StatsAggregationKey(
            string resource,
            string service,
            string operationName,
            string type,
            int httpStatusCode,
            bool isSyntheticsRequest)
        {
            Resource = resource;
            Service = service;
            OperationName = operationName;
            Type = type;
            HttpStatusCode = httpStatusCode;
            IsSyntheticsRequest = isSyntheticsRequest;
        }

        public bool Equals(StatsAggregationKey other)
        {
            return
                Resource == other.Resource
                && Service == other.Service
                && OperationName == other.OperationName
                && Type == other.Type
                && HttpStatusCode == other.HttpStatusCode
                && IsSyntheticsRequest == other.IsSyntheticsRequest;
        }

        public override bool Equals(object obj)
        {
            return obj is StatsAggregationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Resource != null ? Resource.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Service != null ? Service.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OperationName != null ? OperationName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ HttpStatusCode;
                hashCode = (hashCode * 397) ^ IsSyntheticsRequest.GetHashCode();
                return hashCode;
            }
        }
    }
}
