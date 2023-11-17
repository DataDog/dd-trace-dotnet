// <copyright file="ReturnedHeadersAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Mime;
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
    private const string ContentType = "content-type";
    private const string XContentTypeOptions = "x-content-type-options";
    private const string StrictTransportSecurity = "strict-transport-security";
    private const string XForwardedProto = "x-forwarded-proto";
    private const string MaxAgeConst = "max-age=";
    private const string Location = "Location";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReturnedHeadersAnalyzer));

    // Analyze the headers. If the response is HTML, check for X-Content-Type-Options: nosniff. If it
    // is not present, report a vulnerability. When getting the headers, make sure that keys are searched taking
    // int account that can be case insensitive. Exclude vulnerability when return code is one of the ignorable.
#if NETFRAMEWORK
    internal static void Analyze(NameValueCollection responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string protocol)
#else
    internal static void Analyze(IHeaderDictionary responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string protocol)
#endif
    {
        AnalyzeXContentTypeOptionsVulnerability(responseHeaders, integrationId, serviceName, responseCode);
        AnalyzeStrictTransportSecurity(responseHeaders, integrationId, serviceName, responseCode, protocol);
        AnalyzeUnvalidatedRedirect(responseHeaders, integrationId);
    }

#if NETFRAMEWORK
    internal static void AnalyzeXContentTypeOptionsVulnerability(NameValueCollection responseHeaders, IntegrationId integrationId, string serviceName, int responseCode)
#else
    internal static void AnalyzeXContentTypeOptionsVulnerability(IHeaderDictionary responseHeaders, IntegrationId integrationId, string serviceName, int responseCode)
#endif
    {
        try
        {
            IastModule.OnExecutedSinkTelemetry(IastInstrumentedSinks.XContentTypeHeaderMissing);

            if (string.IsNullOrEmpty(serviceName) || IsIgnorableResponseCode((HttpStatusCode)responseCode))
            {
                return;
            }

            string contentTypeValue = responseHeaders[ContentType];
            string contentOptionValue = responseHeaders[XContentTypeOptions];

            if (!IsHtmlResponse(contentTypeValue))
            {
                return;
            }

            LaunchXContentTypeOptionsVulnerability(integrationId, serviceName, contentTypeValue, contentOptionValue);
        }
        catch (Exception error)
        {
            Log.Error(error, $"in Datadog.Trace.Iast.ReturnedHeadersAnalyzer.AnalyzeXContentTypeOptionsVulnerability");
        }
    }

#if NETFRAMEWORK
    internal static void AnalyzeStrictTransportSecurity(NameValueCollection responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string protocol)
#else
    internal static void AnalyzeStrictTransportSecurity(IHeaderDictionary responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string protocol)
#endif
    {
        try
        {
            IastModule.OnExecutedSinkTelemetry(IastInstrumentedSinks.HstsHeaderMissing);

            if (string.IsNullOrEmpty(serviceName) || IsIgnorableResponseCode((HttpStatusCode)responseCode))
            {
                return;
            }

            string contentTypeValue = responseHeaders[ContentType];
            string strictTransportSecurityValue = responseHeaders[StrictTransportSecurity];
            string xForwardedProtoValue = responseHeaders[XForwardedProto];

            if (!IsHtmlResponse(contentTypeValue))
            {
                return;
            }

            LaunchStrictTransportSecurity(integrationId, serviceName, strictTransportSecurityValue, xForwardedProtoValue, protocol);
        }
        catch (Exception error)
        {
            Log.Error(error, $"in Datadog.Trace.Iast.ReturnedHeadersAnalyzer.AnalyzeStrictTransportSecurity");
        }
    }

    private static void LaunchXContentTypeOptionsVulnerability(IntegrationId integrationId, string serviceName, string contentTypeValue, string contentOptionValue)
    {
        if (!string.IsNullOrEmpty(contentTypeValue) && !IsNoSniffContentOptions(contentOptionValue))
        {
            IastModule.OnXContentTypeOptionsHeaderMissing(integrationId, contentOptionValue, serviceName);
        }

        LaunchXContentTypeOptionsVulnerability(integrationId, serviceName, contentTypeValue, contentOptionValue);
    }

    private static void LaunchStrictTransportSecurity(IntegrationId integrationId, string serviceName, string strictTransportSecurityValue, string xForwardedProtoValue, string protocol)
    {
        if (!string.Equals(protocol, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(xForwardedProtoValue, "https", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsValidStrictTransportSecurityValue(strictTransportSecurityValue))
        {
            IastModule.OnStrictTransportSecurityHeaderMissing(integrationId, serviceName);
        }
    }

    // Strict-Transport-Security has a valid value when it starts with max-age followed by a positive number (>0),
    // it can finish there or continue with a semicolon ; and more content.
    private static bool IsValidStrictTransportSecurityValue(string strictTransportSecurityValue)
    {
        if (string.IsNullOrEmpty(strictTransportSecurityValue) || !strictTransportSecurityValue.StartsWith(MaxAgeConst, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = strictTransportSecurityValue.IndexOf(';');

        var maxAge = (index >= 0 ? strictTransportSecurityValue.Substring(MaxAgeConst.Length, index - MaxAgeConst.Length) :
            strictTransportSecurityValue.Substring(MaxAgeConst.Length));

        return ulong.TryParse(maxAge, out var maxAgeInt) && maxAgeInt > 0;
    }

    private static void LaunchStrictTransportSecurity(IntegrationId integrationId, string serviceName, string strictTransportSecurityValue, string xForwardedProtoValue, string protocol)
    {
        if (!string.Equals(protocol, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(xForwardedProtoValue, "https", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsValidStrictTransportSecurityValue(strictTransportSecurityValue))
        {
            IastModule.OnStrictTransportSecurityHeaderMissing(integrationId, serviceName);
        }
    }

    // Strict-Transport-Security has a valid value, when it starts with max-age followed by a positive number (>0),
    // it can finish there or continue with a semicolon ; and more content.
    private static bool IsValidStrictTransportSecurityValue(string strictTransportSecurityValue)
    {
        if (string.IsNullOrEmpty(strictTransportSecurityValue))
        {
            return false;
        }

        if (!strictTransportSecurityValue.StartsWith(MaxAge, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var maxAge = strictTransportSecurityValue.Substring(MaxAge.Length);
        var maxAgeValue = maxAge.Contains(";") ? maxAge.Split(';')[0] : maxAge;

        return (int.TryParse(maxAgeValue, out var maxAgeInt) && maxAgeInt >= 0);
    }

    private static bool IsHtmlResponse(string contentTypeValue)
    {
        if (string.IsNullOrEmpty(contentTypeValue))
        {
            return false;
        }

#if NETCOREAPP
        return contentTypeValue.Contains("text/html", StringComparison.OrdinalIgnoreCase) || contentTypeValue.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
#else
        var contentType = contentTypeValue.ToLowerInvariant();
        return contentType.Contains("text/html") || contentType.Contains("application/xhtml+xml");
#endif
    }

    private static bool IsNoSniffContentOptions(string contentOptionValue)
    {
        return string.Equals(contentOptionValue, "nosniff", StringComparison.OrdinalIgnoreCase);
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

#if NETFRAMEWORK
    internal static void AnalyzeUnvalidatedRedirect(NameValueCollection responseHeaders, IntegrationId integrationId)
#else
    internal static void AnalyzeUnvalidatedRedirect(IHeaderDictionary responseHeaders, IntegrationId integrationId)
#endif
    {
        try
        {
            IastModule.OnExecutedSinkTelemetry(IastInstrumentedSinks.UnvalidatedRedirect);

            var location = responseHeaders[Location];
            if (!string.IsNullOrEmpty(location))
            {
                IastModule.OnUnvalidatedRedirect(location, integrationId);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"in Datadog.Trace.Iast.ReturnedHeadersAnalyzer.AnalyzeUnvalidatedRedirect");
        }
    }
}
