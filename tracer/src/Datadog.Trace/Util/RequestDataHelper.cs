// <copyright file="RequestDataHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Specialized;
using System.Web;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.DuckTypes;
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
    // Gets the values from a request NameValueCollection
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
    /// <summary>
    /// Gets the Uri from the <paramref name="request"/>.
    /// <para>
    /// Note that this will <em>CACHE</em> the <c>Uri</c> of the <paramref name="request"/>
    /// for all future callers (example the customer's application) if we are the first to call <see cref="HttpRequest.Url"/>.
    /// </para>
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> to get the <c>Uri</c> of.</param>
    /// <returns>The <c>Uri</c>; otherwise <see langword="null"/>.</returns>
    internal static Uri? GetUrl(HttpRequest request)
    {
        // UriFormatException can happen if, for example, the request contains the variable "SERVER_NAME" with an invalid value.
        try
        {
            return request.Url;
        }
        catch (Exception ex) when (ex is HttpRequestValidationException || ex is UriFormatException)
        {
            Log.Debug("Error reading request.Url from the request.");
            return null;
        }
    }

    /// <summary>
    /// Builds the Uri from the <paramref name="request"/>.
    /// <para>
    /// Note that this will <em>bypass</em> the caching behavior of the <see cref="HttpRequest.Url"/> property.
    /// </para>
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> to build the <c>Uri</c> from.</param>
    /// <returns>The <c>Uri</c>; otherwise <see langword="null"/>.</returns>
    /// <remarks>While not <em>required</em> to be set this is controlled by <see cref="Configuration.ConfigurationKeys.FeatureFlags.BypassHttpRequestUrlCachingEnabled"/>.</remarks>
    internal static Uri? BuildUrl(HttpRequest request)
    {
        // accessing request.Url will do one of the following:
        // 1. Build the Uri for the HttpRequest IF .Url has not been called previously
        // 2. return the cached Uri that was built when .Url was called previously
        // On some setups that mix Owin and System.Web this can cause issues
        // where the .NET Tracer will access the HttpRequest.Url causing it to be cached
        // and then the customer can't
        var duckRequest = request.DuckCast<IHttpRequest>();

        // this is based on the implementation in .Url with respect to checking WorkerRequest
        // https://referencesource.microsoft.com/#System.Web/HttpRequest.cs,1917
        if (duckRequest.WorkerRequest is not null)
        {
            var path = GetPath(request);
            if (path is not null)
            {
                // comment via https://referencesource.microsoft.com/#System.Web/HttpRequest.cs,1918
                // The Path is accessed in a deferred way to preserve the execution order that existed
                // before the code in BuildUrl was factored out of this property.
                // While evaluating the Path immediately would probably not have an impact on regular execution
                // it might impact error cases. Consider a situation in which some method in workerRequest throws.
                // If we evaluate Path early, then some other method might throw, thus producing a different
                // error behavior for the same conditions. Passing in a Func preserves the old ordering.
                // UriFormatException can happen if, for example, the request contains the variable "SERVER_NAME" with an invalid value
                try
                {
                    return duckRequest.BuildUrl(() => path);
                }
                catch (UriFormatException)
                {
                    Log.Debug("Error building request.Url.");
                    return null;
                }
            }
        }

        Log.Debug("Error calling BuildUrl from the request - falling back to .Url.");
        // fallback to the default implementation
        return GetUrl(request);
    }
#endif
}
