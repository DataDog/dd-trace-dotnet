// <copyright file="HttpMocks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

internal static class HttpMocks
{
    public static HttpRequestMessage CreateMockRequest(
        string url,
        string method,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url));

        foreach (var kvp in headers ?? [])
        {
            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");
        }

        return request;
    }

    public static HttpResponseMessage CreateMockResponse(
        int statusCode,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)statusCode);

        foreach (var kvp in headers ?? [])
        {
            response.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");
        }

        return response;
    }

    public static HttpContent CreateMockContent(string body, string contentType, long? length = null)
    {
        return new StringContent(body, Encoding.UTF8, contentType);
    }

    public static HttpHeaders CreateMockHeaders(Dictionary<string, string> headers)
    {
        var httpHeaders = new TestHttpHeaders();

        foreach (var kvp in headers)
        {
            httpHeaders.TryAddWithoutValidation(kvp.Key, kvp.Value);
        }

        return httpHeaders;
    }

    // Concrete subclass to allow adding any header without category restrictions
    private sealed class TestHttpHeaders : HttpHeaders
    {
    }
}
