// <copyright file="RequestHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal static class RequestHeadersHelper
    {
        internal static readonly IReadOnlyList<string> IpHeaders = new[] { "x-forwarded-for", "x-real-ip", "x-client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via", "true-client-ip" };
        internal static readonly IReadOnlyList<string> MainHeaders = new[] { "user-agent", "referer" };

        internal static IpInfo ExtractIpAndPort(Func<string, string> getHeader, string customIpHeader, IEnumerable<string> customExtraHeaders, bool isSecureConnection, IpInfo peerIpFallback)
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

            var result = IpExtractor.GetRealIpFromValues(ipPotentialValues, isSecureConnection);
            return result ?? peerIpFallback;
        }
    }
}
