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
        internal static readonly IReadOnlyList<string> IpHeaders = new[] { "x-forwarded-for", "x-real-ip", "client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via", "true-client-ip" };
        internal static readonly IReadOnlyList<string> MainHeaders = new[] { "user-agent", "referer" };
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RequestIpExtractor));

        internal static IpInfo ExtractIpAndPort(Func<string, string> getHeader, string customIpHeader, bool isSecureConnection, IpInfo peerIpFallback)
        {
            var ipPotentialValues = new List<string>();
            if (!string.IsNullOrEmpty(customIpHeader))
            {
                var value = getHeader(customIpHeader);
                if (!string.IsNullOrEmpty(value))
                {
                    ipPotentialValues.Add(getHeader(customIpHeader));
                }
            }

            foreach (var headerIp in IpHeaders)
            {
                var potentialIp = getHeader(headerIp);
                if (!string.IsNullOrEmpty(potentialIp))
                {
                    ipPotentialValues.Add(potentialIp);
                }
            }

            if (ipPotentialValues.Count > 1)
            {
                Log.Error("More than one IP header have been found in headers {headers}, no IP will be reported", string.Join(", ", ipPotentialValues));
                return peerIpFallback;
            }

            var result = IpExtractor.GetRealIpFromValue(ipPotentialValues[0], isSecureConnection);
            return result ?? peerIpFallback;
        }

        internal static void AddIpToTags(string peerIpAddress, bool isSecureConnection, Func<string, string> getHeader, string customIpHeader, WebTags tags)
        {
            var peerIp = IpExtractor.ExtractAddressAndPort(peerIpAddress, https: isSecureConnection);
            AddIpToTags(peerIp, isSecureConnection, getHeader, customIpHeader, tags);
        }

        internal static void AddIpToTags(IpInfo peerIp, bool isSecureConnection, Func<string, string> getHeader, string customIpHeader, WebTags tags)
        {
            var ipInfo = ExtractIpAndPort(getHeader, customIpHeader, isSecureConnection, peerIp);
            tags.SetTag(Tags.HttpClientIp, ipInfo.IpAddress);
        }
    }
}
