// <copyright file="RequestDataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
#if NETFRAMEWORK
using System.Collections.Specialized;
using System.Web;
using Datadog.Trace.Logging;
#else
using Microsoft.AspNetCore.Http;
#endif

#nullable enable

namespace Datadog.Trace.Util;

internal static class RequestDataHelper
{
#if NETFRAMEWORK
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestDataHelper));

    // Get the querystring from a HttpRequest
    internal static NameValueCollection? GetQueryString(HttpRequest request)
    {
        try
        {
            return request.QueryString;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading QueryString from the request.");
            return null;
        }
    }
#else
    internal static QueryString GetQueryString(HttpRequest request)
    {
        return request.QueryString;
    }
#endif

#if NETFRAMEWORK
    // Get the cookies from a HttpRequest
    internal static HttpCookieCollection? GetCookies(HttpRequest request)
    {
        try
        {
            return request.Cookies;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading Cookies from the request.");
            return null;
        }
    }
#else
    internal static IRequestCookieCollection GetCookies(HttpRequest request)
    {
        return request.Cookies;
    }
#endif

#if NETFRAMEWORK
    // Get the cookies from a HttpRequest
    internal static NameValueCollection? GetHeaders(HttpRequest request)
    {
        try
        {
            return request.Headers;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading Headers from the request.");
            return null;
        }
    }
#else
    internal static IHeaderDictionary GetHeaders(HttpRequest request)
    {
        return request.Headers;
    }
#endif

#if NETFRAMEWORK
    // Get the cookies from a HttpRequest
    internal static string? GetPath(HttpRequest request)
    {
        try
        {
            return request.Path;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading path from the request.");
            return null;
        }
    }
#else
    internal static string? GetPath(HttpRequest request)
    {
        return request.Path;
    }
#endif

#if NETFRAMEWORK
    // Get the cookies from a HttpRequest
    internal static Uri? GetUrl(HttpRequest request)
    {
        try
        {
            return request.Url;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading request.Url from the request.");
            return null;
        }
    }
#endif
}
