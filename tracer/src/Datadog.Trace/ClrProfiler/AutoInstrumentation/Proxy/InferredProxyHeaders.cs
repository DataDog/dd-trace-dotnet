// <copyright file="InferredProxyHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// HTTP headers that are used to infer the presence of a proxy in front of the application
/// where we can then create an inferred span that represents the proxy.
/// </summary>
internal static class InferredProxyHeaders
{
    /// <summary>
    /// Header that identifies the type of proxy (e.g. "aws-apigateway").
    /// </summary>
    internal const string Name = "x-dd-proxy";

    /// <summary>
    /// Header that contains the start time of the request and the start time of the inferred span (in milliseconds as Unix timestamp).
    /// </summary>
    internal const string StartTime = "x-dd-proxy-request-time-ms";

    /// <summary>
    /// Header that contains the domain name of the proxy.
    /// </summary>
    internal const string Domain = "x-dd-proxy-domain-name";

    /// <summary>
    /// Header that contains the HTTP method of the original request (e.g. "GET").
    /// </summary>
    internal const string HttpMethod = "x-dd-proxy-httpmethod";

    /// <summary>
    /// Header that contains the request path of the original request (e.g. "/api/v1").
    /// </summary>
    internal const string Path = "x-dd-proxy-path";

    /// <summary>
    /// Header that contains the region of the proxy (e.g. "canada central").
    /// </summary>
    internal const string Region = "x-dd-proxy-region";

    /// <summary>
    /// Header that contains the "stage" of the proxy (e.g. "prod").
    /// </summary>
    internal const string Stage = "x-dd-proxy-stage";
}
