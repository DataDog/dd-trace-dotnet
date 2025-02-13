// <copyright file="InferredProxyData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Represents the inferred proxy data that is used to create an inferred span that represents the proxy.
/// </summary>
/// <param name="proxyName">The name/type of the proxy service (e.g. "aws-apigateway")</param>
/// <param name="startTime">When the proxy received the request</param>
/// <param name="domainName">Domain name of the proxy service</param>
/// <param name="httpMethod">HTTP method of the original request</param>
/// <param name="path">Request path at the proxy</param>
/// <param name="stage">Deployment stage name</param>
internal class InferredProxyData(string proxyName, DateTimeOffset startTime, string? domainName, string? httpMethod, string? path, string? stage)
{
    // x-dd-proxy
    public string ProxyName { get; init; } = proxyName;

    // x-dd-proxy-request-time-ms
    public DateTimeOffset StartTime { get; init; } = startTime;

    // x-dd-proxy-domain-name
    public string? DomainName { get; init; } = domainName;

    // x-dd-proxy-httpmethod
    public string? HttpMethod { get; init; } = httpMethod;

    // x-dd-proxy-path
    public string? Path { get; init; } = path;

    // x-dd-proxy-stage
    public string? Stage { get; init; } = stage;
}
