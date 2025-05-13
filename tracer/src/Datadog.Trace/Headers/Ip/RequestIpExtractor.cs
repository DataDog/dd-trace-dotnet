// <copyright file="RequestIpExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Headers.Ip
{
    internal static class RequestIpExtractor
    {
        private static readonly IReadOnlyList<string> IpHeaders =
        [
            "x-forwarded-for",
            "x-real-ip",
            "true-client-ip",
            "x-client-ip",
            "forwarded-for",
            "x-cluster-client-ip",
            "fastly-client-ip",
            "cf-connecting-ip",
            "cf-connecting-ipv6"
        ];

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestIpExtractor));

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
                    extractedCustomIp = IpExtractor.RealIpFromValue(value, isSecureConnection);
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
                var potentialIp = getHeader(headerIp);
                if (!string.IsNullOrEmpty(potentialIp))
                {
                    var ipInfo = IpExtractor.RealIpFromValue(potentialIp, isSecureConnection);
                    if (ipInfo != null)
                    {
                        return ipInfo;
                    }
                }
            }

            return peerIpFallback;
        }

        internal static void AddIpToTags(string? peerIpAddress, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTags tags)
        {
            IpInfo? peerIp = null;
            if (!string.IsNullOrEmpty(peerIpAddress))
            {
                peerIp = IpExtractor.ExtractAddressAndPort(peerIpAddress!, https: isSecureConnection);
            }

            AddIpToTags(peerIp, isSecureConnection, getRequestHeaderFromKey, customIpHeader, tags);
        }

        internal static void AddIpToTags(IpInfo? peerIp, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTags tags)
        {
            var ipInfo = ExtractIpAndPort(getRequestHeaderFromKey, customIpHeader, isSecureConnection, peerIp);

            if (peerIp is not null)
            {
                tags.NetworkClientIp = peerIp.IpAddress;
            }

            if (ipInfo != null)
            {
                tags.HttpClientIp = ipInfo.IpAddress;
            }
        }
    }
}
