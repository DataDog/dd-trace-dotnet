// <copyright file="CookieAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using System;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Datadog.Trace.IAST
{
    internal static class CookieAnalyzer
    {
        private static Uri _dummyUri = new Uri("http://127.0.0.1");

        // Extract the cookie information of a request from a IHeaderDictionary
        public static void AnalyzeCookies(IHeaderDictionary headers, IntegrationId integrationId)
        {
            if (!headers.TryGetValue(Microsoft.Net.Http.Headers.HeaderNames.SetCookie, out var cookieHeaderValues))
            {
                return;
            }

            foreach (var cookieHeaderValue in cookieHeaderValues)
            {
                AnalyzeCookie(cookieHeaderValue, integrationId);
            }
        }

        private static void AnalyzeCookie(string cookieHeaderValue, IntegrationId integrationId)
        {
            if (!string.IsNullOrEmpty(cookieHeaderValue))
            {
                var cookieContainer = new CookieContainer();
                cookieContainer.SetCookies(_dummyUri, cookieHeaderValue);
                var parsedCookie = cookieContainer.GetCookies(_dummyUri);

                if (parsedCookie.Count == 1)
                {
                    var cookie = parsedCookie[0];
                    var value = cookie.Value;

                    // Insecure cookies with empty values are allowed
                    if (!string.IsNullOrEmpty(value))
                    {
                        // CookieContainer does not parse the samesite attribute of the cookie.
                        // A cookie has the same site attribute ok only if it defines the samesite value as strict. Lax or none are invalid. The default value is invalid also.
                        bool sameSiteOk = cookieHeaderValue.ToLower().Contains("samesite=strict");
                        ReportVulnerabilities(integrationId, cookie, sameSiteOk);
                    }
                }
            }
        }

        private static void ReportVulnerabilities(IntegrationId integrationId, Cookie cookie, bool sameSiteOk)
        {
            if (!sameSiteOk)
            {
                IastModule.OnNoSamesiteCookie(integrationId, cookie.Name);
            }

            if (!cookie.HttpOnly)
            {
                IastModule.OnNoHttpOnlyCookie(integrationId, cookie.Name);
            }

            if (!cookie.Secure)
            {
                IastModule.OnInsecureCookie(integrationId, cookie.Name);
            }
        }
    }
}

#endif
