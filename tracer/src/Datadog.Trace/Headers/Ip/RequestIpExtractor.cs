// <copyright file="RequestIpExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Headers.Ip
{
    internal static class RequestIpExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestIpExtractor));

        private static readonly IReadOnlyList<HeaderExtractor> IpHeaders =
        [
            new("forwarded", ForwardedHeaderComponentParser),
            new("x-forwarded-for", DefaultHeaderComponentParser),
            new("x-real-ip", DefaultHeaderComponentParser),
            new("true-client-ip", DefaultHeaderComponentParser),
            new("x-client-ip", DefaultHeaderComponentParser),
            new("forwarded-for", DefaultHeaderComponentParser),
            new("x-cluster-client-ip", DefaultHeaderComponentParser),
            new("fastly-client-ip", DefaultHeaderComponentParser),
            new("cf-connecting-ip", DefaultHeaderComponentParser),
            new("cf-connecting-ipv6", DefaultHeaderComponentParser),
        ];

        // Regex to extract for= value (quoted or unquoted)
        private static readonly Regex ForRegex = new Regex(@"for=\s*(?:""(?<val>[^""]+)""|(?<val>[^;,\s]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static string? DefaultHeaderComponentParser(string? value) => value?.Trim();

        internal static string? ForwardedHeaderComponentParser(string? value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                // Extract the "for" part from this segment
                var match = ForRegex.Match(value);
                if (!match.Success)
                {
                    return null;
                }

                return match.Groups["val"].Success ? match.Groups["val"].Value : null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while trying to parse a Forwarded header value: {Value}", value);
                return null;
            }
        }

        /// <summary>
        /// Extract ip and port following https://datadoghq.atlassian.net/wiki/spaces/SAAL/pages/2118779066/Client+IP+addresses+resolution
        /// </summary>
        /// <param name="getHeader">way to extract the header core/fmk</param>
        /// <param name="customIpHeader">if a custom header has been set</param>
        /// <param name="isSecureConnection">if it's a secure connection</param>
        /// <param name="peerIpFallback">peer ip fallback, can be null if none has been found</param>
        /// <returns>the found ip, custom ip if a header has been specified, public / private ip in the order of headers above, if nothing found and no custom header is specified, falls back on the peer ip</returns>
        internal static IpInfo? ExtractIpAndPort(Func<string, string> getHeader, string customIpHeader, bool isSecureConnection, IpInfo? peerIpFallback)
        {
            IpInfo? extractedCustomIp = null;
            if (!string.IsNullOrEmpty(customIpHeader))
            {
                var value = getHeader(customIpHeader);
                if (!string.IsNullOrEmpty(value))
                {
                    extractedCustomIp = IpExtractor.RealIpFromValue(value, isSecureConnection, DefaultHeaderComponentParser);
                    if (extractedCustomIp == null)
                    {
                        Log.Debug("A custom header for ip with value {Value} was configured but no correct ip could be extracted", value);
                    }
                }
                else
                {
                    Log.Debug("A custom header for ip {CustomIpHeader} was configured but there was no such header in the request", customIpHeader);
                }

                // don't fall back on other headers as per requirements
                return extractedCustomIp;
            }

            foreach (var headerIp in IpHeaders)
            {
                var potentialIp = getHeader(headerIp.HeaderName);
                if (!string.IsNullOrEmpty(potentialIp))
                {
                    var ipInfo = IpExtractor.RealIpFromValue(potentialIp, isSecureConnection, headerIp.Extractor);
                    if (ipInfo != null)
                    {
                        return ipInfo;
                    }
                }
            }

            return peerIpFallback;
        }

        internal static void AddIpToTags(string? peerIpAddress, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTagsWithoutIpTracking tags)
        {
            IpInfo? peerIp = null;
            if (!string.IsNullOrEmpty(peerIpAddress))
            {
                peerIp = IpExtractor.ExtractAddressAndPort(peerIpAddress!, https: isSecureConnection);
            }

            AddIpToTags(peerIp, isSecureConnection, getRequestHeaderFromKey, customIpHeader, tags);
        }

        internal static void AddIpToTags(IpInfo? peerIp, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTagsWithoutIpTracking tags)
        {
            var ipInfo = ExtractIpAndPort(getRequestHeaderFromKey, customIpHeader, isSecureConnection, peerIp);

            if (tags is WebTags withIpTracking)
            {
                if (peerIp is not null)
                {
                    withIpTracking.NetworkClientIp = peerIp.IpAddress;
                }

                if (ipInfo != null)
                {
                    withIpTracking.HttpClientIp = ipInfo.IpAddress;
                }
            }
            else
            {
                if (peerIp is not null)
                {
                    tags.SetTag(Tags.NetworkClientIp, peerIp.IpAddress);
                }

                if (ipInfo != null)
                {
                    tags.SetTag(Tags.HttpClientIp, ipInfo.IpAddress);
                }
            }
        }

        internal record struct HeaderExtractor(string HeaderName, Func<string?, string?> Extractor)
        {
        }
    }
}
