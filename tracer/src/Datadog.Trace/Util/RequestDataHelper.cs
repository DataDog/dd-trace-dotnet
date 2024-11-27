// <copyright file="RequestDataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
#if NETFRAMEWORK
using System.Collections.Specialized;
using System.Reflection;
using System.Web;
using System.Web.ModelBinding;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;
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
    // Get the values from a request NameValueCollection
    internal static string[]? GetNameValueCollectionValues(NameValueCollection queryString, string key)
    {
        try
        {
            return queryString.GetValues(key);
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading NameValueCollection values from the request.");
            return null;
        }
    }

    internal static string? GetNameValueCollectionValue(NameValueCollection queryString, string key)
    {
        try
        {
            return queryString[key];
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading NameValueCollection value from the request.");
            return null;
        }
    }

    // Get form from a request
    internal static NameValueCollection? GetForm(HttpRequest request)
    {
        try
        {
            return request.Form;
        }
        catch (HttpException)
        {
            Log.Debug("Error reading Form (body) from the request.");
            return null;
        }
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
    // Get the headers from a HttpRequest
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
    // Get the path from a HttpRequest
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
    private static Uri? TryGetRequestUrl(HttpRequest request, string logMessage)
    {
        try
        {
            return request.Url;
        }
        catch (HttpRequestValidationException)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Error reading request.Url from the request. {Message}", logMessage);
            }

            return null;
        }
    }

    // Get the url from a HttpRequest
    internal static Uri? GetUrl(HttpRequest request)
    {
        // TODO do we need to lock(request) here?
        var urlField = typeof(HttpRequest).GetField("_url", BindingFlags.NonPublic | BindingFlags.Instance);

        if (urlField is null)
        {
            return TryGetRequestUrl(request, "Failed to reflect into _url of HttpRequest, falling back to Url property.");
        }

        var urlValueBefore = urlField.GetValue(request) as Uri;

        // if the .Url has already been accessed by something else
        // then the _url field will have already been cached
        // in this instance we shouldn't reset the field to ensure we
        // don't introduce some side-effect
        // however .Url doesn't just check _url, so we should still call the actual property
        if (urlValueBefore is not null)
        {
            return TryGetRequestUrl(request, "_url was already accessed by something else.");
        }

        // we are first callers of the .Url property
        // we should cache this value and then reset the _url field
        // to ensure we don't introduce side-effects
        // we saw this happen with customers using Owin middleware
        var url = TryGetRequestUrl(request, "_url was not accessed by anything else.");

        // reset _url
        try
        {
            // will happen regardless whether we got a value or not from .Url
            urlField?.SetValue(request, null);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error resetting request.Url.");
            return url;
        }

        return url;
    }
#endif
}
