// <copyright file="ReturnedHeadersAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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
    private static string[] headerInjectionExceptions = new string[] { "location", "Sec-WebSocket-Location", "Sec-WebSocket-Accept", "Upgrade", "Connection" };

    // Analyze the headers. If the response is HTML, check for X-Content-Type-Options: nosniff. If it
    // is not present, report a vulnerability. When getting the headers, make sure that keys are searched taking
    // int account that can be case insensitive. Exclude vulnerability when return code is one of the ignorable.
#if NETFRAMEWORK
    internal static void Analyze(NameValueCollection responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string? protocol)
#else
    internal static void Analyze(IHeaderDictionary responseHeaders, IntegrationId integrationId, string serviceName, int responseCode, string? protocol)
#endif
    {
        AnalyzeXContentTypeOptionsVulnerability(responseHeaders, integrationId, serviceName, responseCode);
        if (protocol is not null)
        {
            AnalyzeStrictTransportSecurity(responseHeaders, integrationId, serviceName, responseCode, protocol);
        }

        AnalyzeUnvalidatedRedirect(responseHeaders, integrationId);
        AnalyzeHeaderInjectionVulnerability(responseHeaders, integrationId);
    }

    // In header injections, we should exclude some headers to prevent false positives:
    // location: it is already reported in UNVALIDATED_REDIRECT vulnerability detection.
    // Sec-WebSocket-Location, Sec-WebSocket-Accept, Upgrade, Connection: Usually the framework gets info from request
    // access-control-allow-*: when the source of the tainted range is the request header origin or access-control-request-headers
    // set-cookie: We should ignore set-cookie header if the source of all the tainted ranges are cookies
    // "vary: origin"
    // We should exclude the injection when the tainted string only has one range which comes from a request header with the same name that the header that we are checking in the response.
    // Headers could store sensitive information, we should redact whole <header_value> if:
    // <header_name> matches with a RegExp
    // <header_value> matches with  a RegExp
    // We should redact the sensitive information from the evidence when:
    // Tainted range is considered sensitive value

#if NETFRAMEWORK
    private static void AnalyzeHeaderInjectionVulnerability(NameValueCollection responseHeaders, IntegrationId integrationId)
#else
    private static void AnalyzeHeaderInjectionVulnerability(IHeaderDictionary responseHeaders, IntegrationId integrationId)
#endif
    {
        try
        {
            IastModule.OnExecutedSinkTelemetry(IastInstrumentedSinks.HeaderInjection);
            var currentSpan = (Tracer.Instance.ActiveScope as Scope)?.Span;
            var iastRequestContext = currentSpan?.Context?.TraceContext?.IastRequestContext;

            if (iastRequestContext is null)
            {
                return;
            }

#if NETFRAMEWORK
            for (int i = 0; i < responseHeaders.Count; i++)
            {
                var headerKey = responseHeaders.GetKey(i);
                var headerValues = responseHeaders.GetValues(i);
#else
            foreach (var headerKeyValue in responseHeaders)
            {
                var headerKey = headerKeyValue.Key;
                var headerValues = headerKeyValue.Value;
#endif
                if (string.IsNullOrWhiteSpace(headerKey) || IsHeaderInjectionException(headerKey))
                {
                    continue;
                }

                foreach (var headerValue in headerValues)
                {
                    if (IsHeaderInjectionVulnerable(headerKey, headerValue, iastRequestContext))
                    {
                        IastModule.OnHeaderInjection(integrationId, headerKey, headerValue);
                    }
                }
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"Error in {nameof(ReturnedHeadersAnalyzer)}.{nameof(AnalyzeHeaderInjectionVulnerability)}");
        }
    }

    private static bool IsHeaderInjectionVulnerable(string headerKey, string headerValue, IastRequestContext iastrequestContext)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        // Exception for Vary
        if (headerKey.Equals("vary", StringComparison.OrdinalIgnoreCase) && headerValue.Equals("origin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var taintedValue = iastrequestContext.GetTainted(headerValue);

        if (taintedValue is null ||
            taintedValue.Ranges.Length == 0 ||
            (headerKey.Equals("set-cookie", StringComparison.OrdinalIgnoreCase) && OnlyContainsCookieValueSources(taintedValue)) ||
            (headerKey.StartsWith("access-control-allow-", StringComparison.OrdinalIgnoreCase) && ComesFromOriginHeader(taintedValue)) ||
            IsPropagationHeader(headerKey, taintedValue))
        {
            return false;
        }

        return true;
    }

    private static bool IsHeaderInjectionException(string headerKey)
    {
        bool isHeaderInjectionException = false;
        foreach (var excludeType in headerInjectionExceptions)
        {
            if (excludeType.Equals(headerKey, StringComparison.OrdinalIgnoreCase))
            {
                isHeaderInjectionException = true;
                break;
            }
        }

        return (isHeaderInjectionException);
    }

    private static bool OnlyContainsCookieValueSources(TaintedObject taintedValue)
    {
        foreach (var range in taintedValue.Ranges)
        {
            if (range.Source is not null && range.Source.Origin != SourceType.CookieValue)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPropagationHeader(string headerName, TaintedObject taintedValue)
    {
        return (
            taintedValue.Ranges.Length == 1 &&
            taintedValue.Ranges[0].Source is not null &&
            taintedValue.Ranges[0].Source!.Origin == SourceType.RequestHeaderValue &&
            string.Equals(taintedValue.Ranges[0].Source!.Name, headerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ComesFromOriginHeader(TaintedObject taintedValue)
    {
        foreach (var range in taintedValue.Ranges)
        {
            if (range.Source is not null && (range.Source.Origin != SourceType.RequestHeaderValue
                || (!string.Equals(range.Source.Name, "origin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(range.Source.Name, "access-control-request-headers", StringComparison.OrdinalIgnoreCase))))
            {
                return false;
            }
        }

        return true;
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
