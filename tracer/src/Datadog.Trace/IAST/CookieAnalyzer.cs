// <copyright file="CookieAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
#else
using System.Web;
#endif

namespace Datadog.Trace.IAST;

internal static class CookieAnalyzer
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CookieAnalyzer));

#if NETFRAMEWORK
    public static void AnalyzeCookies(HttpCookieCollection cookies, IntegrationId integrationId)
    {
        try
        {
            foreach (string cookieKey in cookies)
            {
                var cookie = cookies[cookieKey];
                ReportVulnerabilities(integrationId, cookie);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(CookieAnalyzer)}.{nameof(AnalyzeCookies)}.net461 exception");
        }
    }

    private static void ReportVulnerabilities(IntegrationId integrationId, HttpCookie cookie)
    {
        var name = cookie.Name;
        var value = cookie.Values[0];

        // Insecure cookies with empty values are allowed
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        string? samesiteCookie = cookie.Values["SameSite"];
        if (samesiteCookie?.Equals("Strict", System.StringComparison.InvariantCultureIgnoreCase) is not true)
        {
            IastModule.OnNoSamesiteCookie(integrationId, name);
        }

        if (!cookie.HttpOnly)
        {
            IastModule.OnNoHttpOnlyCookie(integrationId, name);
        }

        if (!cookie.Secure)
        {
            IastModule.OnInsecureCookie(integrationId, name);
        }
    }
#endif

#if !NETFRAMEWORK
    // Extract the cookie information of a request from a IHeaderDictionary
    public static void AnalyzeCookies(IHeaderDictionary headers, IntegrationId integrationId)
    {
        try
        {
            if (!headers.TryGetValue(HeaderNames.SetCookie, out var cookieHeaderValues))
            {
                return;
            }

            foreach (var cookieHeaderValue in cookieHeaderValues)
            {
                AnalyzeCookie(cookieHeaderValue, integrationId);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(CookieAnalyzer)}.{nameof(AnalyzeCookies)}.netcore exception");
        }
    }

    private static void AnalyzeCookie(string cookieHeaderValue, IntegrationId integrationId)
    {
        if (!string.IsNullOrEmpty(cookieHeaderValue))
        {
            var cookieHeader = SetCookieHeaderValue.Parse(cookieHeaderValue);
            ReportVulnerabilities(integrationId, cookieHeader);
        }
    }

    private static void ReportVulnerabilities(IntegrationId integrationId, SetCookieHeaderValue cookie)
    {
        var name = cookie.Name.ToString();
        var value = cookie.Value.ToString();

        // Insecure cookies with empty values are allowed
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (cookie.SameSite != Microsoft.Net.Http.Headers.SameSiteMode.Strict)
        {
            IastModule.OnNoSamesiteCookie(integrationId, name);
        }

        if (!cookie.HttpOnly)
        {
            IastModule.OnNoHttpOnlyCookie(integrationId, name);
        }

        if (!cookie.Secure)
        {
            IastModule.OnInsecureCookie(integrationId, name);
        }
    }
#endif
}
