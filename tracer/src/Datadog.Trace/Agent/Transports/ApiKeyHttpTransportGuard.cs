// <copyright file="ApiKeyHttpTransportGuard.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Agent.Transports;

internal static class ApiKeyHttpTransportGuard
{
    internal const string ApiKeyHeaderName = "DD-API-KEY";

    public static bool IsPlaintextLoopback(Uri endpoint)
        => string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && endpoint.IsLoopback;

    public static void RejectLateApiKeyHeader(string headerName)
    {
        if (string.Equals(headerName, ApiKeyHeaderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiKeyHttpTransportException("DD-API-KEY must be configured when constructing the request factory.");
        }
    }

    public static void EnsureSafeEndpoint(Uri endpoint)
    {
        if (string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            IsPlaintextLoopback(endpoint))
        {
            return;
        }

        throw new ApiKeyHttpTransportException(
            "Refusing to send DD-API-KEY unless the endpoint uses HTTPS or loopback HTTP.");
    }

    public static void EnsureSafe(Uri endpoint, bool isProxyDisabled, bool redirectsDisabled)
    {
        EnsureSafeEndpoint(endpoint);

        if (redirectsDisabled && (!IsPlaintextLoopback(endpoint) || isProxyDisabled))
        {
            return;
        }

        throw new ApiKeyHttpTransportException(
            "Refusing to send DD-API-KEY unless automatic redirects are disabled and the endpoint uses HTTPS or direct loopback HTTP.");
    }
}
