// <copyright file="StatsAggregationKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent;

internal readonly record struct StatsAggregationKey
{
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
        ulong peerTagsHash,
        ulong additionalMetricTagsHash)
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
        AdditionalMetricTagsHash = additionalMetricTagsHash;
    }

    public string Resource { get; }

    public string Service { get; }

    public string OperationName { get; }

    public string Type { get; }

    public int HttpStatusCode { get; }

    public bool IsSyntheticsRequest { get; }

    public bool IsError { get; }

    public bool IsTopLevel { get; }

    public string SpanKind { get; }

    public bool IsTraceRoot { get; }

    public string HttpMethod { get; }

    public string HttpEndpoint { get; }

    public string GrpcStatusCode { get; }

    public string ServiceSource { get; }

    public ulong PeerTagsHash { get; }

    public ulong AdditionalMetricTagsHash { get; init; }
}
