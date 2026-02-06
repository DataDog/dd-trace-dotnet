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
        public readonly bool IsError;
        public readonly bool IsTopLevel;

        // Constructs a StatsAgregationKey that represents the aggregation key used by Datadog,
        // which does not include IsError and IsTopLevel, since these should be part of the same timeseries
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
            IsError = false;
            IsTopLevel = false;
        }

        // Constructs a StatsAgregationKey that represents the aggregation key used by OpenTelemetry,
        // which considers IsError and IsTopLevel since these should be considered as unique timeseries
        public StatsAggregationKey(
            string resource,
            string service,
            string operationName,
            string type,
            int httpStatusCode,
            bool isSyntheticsRequest,
            bool isError,
            bool isTopLevel)
        {
            Resource = resource;
            Service = service;
            OperationName = operationName;
            Type = type;
            HttpStatusCode = httpStatusCode;
            IsSyntheticsRequest = isSyntheticsRequest;
            IsError = isError;
            IsTopLevel = isTopLevel;
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
                && IsTopLevel == other.IsTopLevel;
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
                hashCode = (hashCode * 397) ^ IsError.GetHashCode();
                hashCode = (hashCode * 397) ^ IsTopLevel.GetHashCode();
                return hashCode;
            }
        }
    }
}
