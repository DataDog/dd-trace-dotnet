// <copyright file="InsecureAuthAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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
            if (!headers.TryGetValue(headerName, out var authHeader) || StringValues.IsNullOrEmpty(authHeader))
            {
                return;
            }

            // The StringValues can have multiple values because of a concatenation of multiple Authorization headers
            // This is not a standard practice for the Authorization header but can theoretically happen.
            if (authHeader.Count == 1)
            {
                var detectedScheme = InsecureSchemeDetected(authHeader);
                if (detectedScheme is null)
                {
                    return;
                }

                IastModule.OnInsecureAuthProtocol($"{headerName}: {detectedScheme}", integrationId);
            }
            else
            {
                foreach (var header in authHeader)
                {
                    var detectedScheme = InsecureSchemeDetected(header);
                    if (detectedScheme is null)
                    {
                        continue;
                    }

                    IastModule.OnInsecureAuthProtocol($"{headerName}: {detectedScheme}", integrationId);
                }
            }
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

            // Check for Authorization header
            const string headerName = "Authorization";
            var authHeader = headers[headerName];
            if (string.IsNullOrEmpty(authHeader))
            {
                return;
            }

            // The auth header can be a concatenation of multiple Authorization headers (concatenated by a comma)
            // This is not a standard practice for the Authorization header but can theoretically happen.
            var authHeaders = authHeader.Split(',');
            foreach (var header in authHeaders)
            {
                var detectedScheme = InsecureSchemeDetected(header);
                if (detectedScheme is null)
                {
                    continue;
                }

                IastModule.OnInsecureAuthProtocol($"{headerName}: {detectedScheme}", integrationId);
            }
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
        // Trim whitespaces from the beginning of the string
        var i = 0;
        while (i < authHeader.Length && char.IsWhiteSpace(authHeader[i]))
        {
            i++;
        }

        // Check if the string starts with "Basic" (case insensitive)
        if (i + 5 < authHeader.Length &&
            (authHeader[i] == 'B' || authHeader[i] == 'b') &&
            (authHeader[i + 1] == 'A' || authHeader[i + 1] == 'a') &&
            (authHeader[i + 2] == 'S' || authHeader[i + 2] == 's') &&
            (authHeader[i + 3] == 'I' || authHeader[i + 3] == 'i') &&
            (authHeader[i + 4] == 'C' || authHeader[i + 4] == 'c'))
        {
            return "Basic";
        }

        // Check if the string starts with "Digest" (case insensitive)
        if (i + 6 < authHeader.Length &&
            (authHeader[i] == 'D' || authHeader[i] == 'd') &&
            (authHeader[i + 1] == 'I' || authHeader[i + 1] == 'i') &&
            (authHeader[i + 2] == 'G' || authHeader[i + 2] == 'g') &&
            (authHeader[i + 3] == 'E' || authHeader[i + 3] == 'e') &&
            (authHeader[i + 4] == 'S' || authHeader[i + 4] == 's') &&
            (authHeader[i + 5] == 'T' || authHeader[i + 5] == 't'))
        {
            return "Digest";
        }

        return null;
    }
}
