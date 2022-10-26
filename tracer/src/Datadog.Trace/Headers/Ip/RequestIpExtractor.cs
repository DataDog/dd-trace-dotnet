// <copyright file="RequestIpExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Headers.Ip
{
    internal static class RequestIpExtractor
    {
        private static readonly IReadOnlyList<string> IpHeaders = new[] { "x-forwarded-for", "x-real-ip", "client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via", "true-client-ip" };
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestIpExtractor));

        internal static IpInfo ExtractIpAndPort(Func<string, string> getHeader, string customIpHeader, bool isSecureConnection, IpInfo peerIpFallback)
        {
            IpInfo extractedCustomIp = null;
            if (!string.IsNullOrEmpty(customIpHeader))
            {
                var value = getHeader(customIpHeader);
                if (!string.IsNullOrEmpty(value))
                {
                    extractedCustomIp = IpExtractor.RealIpFromValue(value, isSecureConnection);
                    if (extractedCustomIp == null)
                    {
                        Log.Warning("A custom header for ip with value {value} was configured but no correct ip could be extracted", value);
                    }

                    return extractedCustomIp;
                }

                Log.Warning("A custom header for ip {customIpHeader} was configured but there was no such header in the request", customIpHeader);
            }

            string potentialIp = null;
            foreach (var headerIp in IpHeaders)
            {
                var headerValue = getHeader(headerIp);
                if (!string.IsNullOrEmpty(headerValue))
                {
                    if (!string.IsNullOrEmpty(potentialIp) || !string.IsNullOrEmpty(customIpHeader))
                    {
                        // dont log more than debug level. some proxies have several ip headers and we end up with millions of logs.
                        Log.Debug("Multiple ip headers have been found, none will be reported");
                        return null;
                    }

                    potentialIp = headerValue;
                }
            }

            return extractedCustomIp ?? (!string.IsNullOrEmpty(potentialIp) ? IpExtractor.RealIpFromValue(potentialIp, isSecureConnection) : peerIpFallback);
        }

        internal static void AddIpToTags(string peerIpAddress, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTags tags)
        {
            var peerIp = IpExtractor.ExtractAddressAndPort(peerIpAddress, https: isSecureConnection);
            AddIpToTags(peerIp, isSecureConnection, getRequestHeaderFromKey, customIpHeader, tags);
        }

        internal static void AddIpToTags(IpInfo peerIp, bool isSecureConnection, Func<string, string> getRequestHeaderFromKey, string customIpHeader, WebTags tags)
        {
            var ipInfo = ExtractIpAndPort(getRequestHeaderFromKey, customIpHeader, isSecureConnection, peerIp);
            tags.NetworkClientIp = peerIp?.IpAddress;

            if (ipInfo != null)
            {
                tags.HttpClientIp = ipInfo.IpAddress;
            }
        }
    }
}
