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
            const string headerName = HeaderNames.Authorization;
            if (!headers.TryGetValue(headerName, out var authHeader))
            {
                return;
            }

            var detectedScheme = InsecureSchemeDetected(authHeader);
            if (detectedScheme is null)
            {
                return;
            }

            IastModule.OnInsecureAuthProtocol($"{headerName}: {detectedScheme}", integrationId);
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
            // Check for Authorization header
            const string headerName = "Authorization";
            var authHeader = headers[headerName];
            if (string.IsNullOrEmpty(authHeader))
            {
                return;
            }

            if (IgnoredStatusCode(statusCode))
            {
                return;
            }

            var detectedScheme = InsecureSchemeDetected(authHeader);
            if (detectedScheme is null)
            {
                return;
            }

            IastModule.OnInsecureAuthProtocol($"{headerName}: {detectedScheme}", integrationId);
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
        return statusCode >= 400;
    }

    private static string? InsecureSchemeDetected(string authHeader)
    {
        var insecureSchemes = new[] { "Basic", "Digest" };

        // The auth header can be a concatenation of multiple Authorization headers (concatenated by a comma)
        // This is not a standard practice for the Authorization header but can theoretically happen.
        var elements = authHeader.Split(',');
        foreach (var element in elements)
        {
            var scheme = element.Trim(' ').Split(' ')[0];
            var detectedScheme = insecureSchemes.FirstOrDefault(s => s.Equals(scheme, StringComparison.OrdinalIgnoreCase));
            if (detectedScheme is not null)
            {
                return detectedScheme;
            }
        }

        return null;
    }
}
