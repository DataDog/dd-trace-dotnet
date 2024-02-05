// <copyright file="InsecureAuthAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
#endif

namespace Datadog.Trace.Iast;

#nullable enable

internal static class InsecureAuthAnalyzer
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InsecureAuthAnalyzer));

#if !NETFRAMEWORK
    public static void AnalyzeInsecureAuth(IHeaderDictionary headers, IntegrationId integrationId, int statusCode)
    {
        try
        {
            if (IgnoredStatusCode(statusCode))
            {
                return;
            }

            // Check for Authorization header
            if (!headers.TryGetValue(HeaderNames.Authorization, out var authHeader))
            {
                return;
            }

            if (!IsInsecureProtocol(authHeader))
            {
                return;
            }

            IastModule.OnInsecureAuthProtocol(authHeader, integrationId);
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(InsecureAuthAnalyzer)}.{nameof(AnalyzeInsecureAuth)}.netcore exception");
        }
    }
#endif

#if NETFRAMEWORK
    public static void AnalyzeInsecureAuth(System.Collections.Specialized.NameValueCollection headers, IntegrationId integrationId, int statusCode)
    {
        try
        {
            if (IgnoredStatusCode(statusCode))
            {
                return;
            }

            // Get Authorization header
            var authHeader = headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader))
            {
                return;
            }

            if (!IsInsecureProtocol(authHeader))
            {
                return;
            }

            IastModule.OnInsecureAuthProtocol(authHeader, integrationId);
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(InsecureAuthAnalyzer)}.{nameof(AnalyzeInsecureAuth)}.net461 exception");
        }
    }
#endif

    private static bool IgnoredStatusCode(int statusCode)
    {
        // To minimize false positives when we get auth credentials to a page that doesn't exist
        // (e.g. happens with vulnerability scanners), we'll just ignore this vulnerability when
        // there is no success response.
        return statusCode >= 300;
    }

    private static bool IsInsecureProtocol(string authHeader)
    {
        var insecureProtocols = new[] { "basic", "digest" };

        // Case insensitive comparison
        return insecureProtocols.Contains(authHeader.Split(' ')[0], StringComparer.OrdinalIgnoreCase);
    }
}
