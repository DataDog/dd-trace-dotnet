// <copyright file="QueryStringHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Specialized;
using System.Web;
using Datadog.Trace.Logging;
#else
using Microsoft.AspNetCore.Http;
#endif

#nullable enable

namespace Datadog.Trace.Util;

internal static class QueryStringHelper
{
#if NETFRAMEWORK
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QueryStringHelper));

    // Get the querystring from a HttpRequest
    internal static NameValueCollection? GetQueryString(HttpRequest request)
    {
        try
        {
            return request.QueryString;
        }
        catch (HttpRequestValidationException)
        {
            Log.Debug("Error reading request QueryString.");
            return null;
        }
    }
#else
    internal static QueryString GetQueryString(HttpRequest request)
    {
        return request.QueryString;
    }
#endif
}
