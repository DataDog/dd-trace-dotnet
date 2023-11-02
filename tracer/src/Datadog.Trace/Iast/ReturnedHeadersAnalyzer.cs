// <copyright file="ReturnedHeadersAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#endif
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

#nullable enable
namespace Datadog.Trace.Iast;

internal static class ReturnedHeadersAnalyzer
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReturnedHeadersAnalyzer));

#if NETFRAMEWORK
#else
    // Analyze the headers. If the response is HTML, check for X-Content-Type-Options: nosniff. If it
    // is not present, report a vulnerability. When getting the headers, make sure that keys are searched taking
    // int account that can be case insensitive.
    internal static void Analyze(IHeaderDictionary responseHeaders, IntegrationId integrationId, string serviceName)
    {
        try
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                return;
            }

            IastModule.OnExecutedSinkTelemetry(IastInstrumentedSinks.XContentTypeHeaderMissing);

            string contentTypeValue = string.Empty;
            string contentOptionValue = string.Empty;

            // headers can be case insensitive
            foreach (var header in responseHeaders)
            {
                if (header.Key.ToLowerInvariant() == "content-type")
                {
                    contentTypeValue = header.Value;
                }

                if (header.Key.ToLowerInvariant() == "x-content-type-options")
                {
                    contentOptionValue = header.Value;
                }
            }

            if (IsHtmlResponse(contentTypeValue) && !IsNoSniffContentOptions(contentOptionValue))
            {
                IastModule.OnXContentTypeOptionsHeaderMissing(integrationId, contentOptionValue, serviceName);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(ReturnedHeadersAnalyzer)}.{nameof(Analyze)} exception");
        }
    }
#endif

    private static bool IsHtmlResponse(string contentTypeValue)
    {
        if (string.IsNullOrEmpty(contentTypeValue))
        {
            return false;
        }

        var contentType = contentTypeValue.ToLowerInvariant();
        return contentType == ("text/html") || contentType == ("application/xhtml+xml");
    }

    private static bool IsNoSniffContentOptions(string contentOptionValue)
    {
        if (contentOptionValue == null)
        {
            return false;
        }

        return contentOptionValue.ToLowerInvariant() == "nosniff";
    }

    public static bool IsIgnorableResponseCode(HttpStatusCode httpStatus)
    {
        switch (httpStatus)
        {
            case HttpStatusCode.Moved:
            case HttpStatusCode.Redirect:
            case HttpStatusCode.NotModified:
            case HttpStatusCode.TemporaryRedirect:
            case HttpStatusCode.NotFound:
            case HttpStatusCode.Gone:
            case HttpStatusCode.InternalServerError:
                return true;
            default:
                return false;
        }
    }
}
