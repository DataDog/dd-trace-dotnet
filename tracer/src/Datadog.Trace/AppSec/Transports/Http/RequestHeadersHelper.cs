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
        internal static readonly string[] IpHeaders = new string[] { "x-forwarded-for", "x-real-ip", "client-ip", "x-forwarded", "x-cluster-client-ip", "forwarded-for", "forwarded", "via", "true-client-ip" };
        internal static readonly IEnumerable<string> OtherHeaders = new string[] { "user-agent", "referer" };

        internal static ExtractedHeadersAndIpInfos ExtractHeadersIpAndPort(Func<string, string> getHeader, string customIpHeader, string[] extraHeaders, bool isSecureConnection, IpInfo peerIpFallback)
        {
            var headersDic = new Dictionary<string, string>();
            var ipPotentialValues = new List<string>();

            foreach (var headerToSend in OtherHeaders)
            {
                var headerValue = getHeader(headerToSend);
                if (!string.IsNullOrEmpty(headerValue))
                {
                    headersDic.Add(headerToSend, headerValue);
                }
            }

            foreach (var headerToSend in extraHeaders)
            {
                var headerValue = getHeader(headerToSend);
                if (!string.IsNullOrEmpty(headerValue))
                {
                    headersDic.Add(headerToSend, getHeader(headerToSend));
                }
            }

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
                headersDic.Add(headerIp, potentialIp);
                if (!string.IsNullOrEmpty(potentialIp))
                {
                    ipPotentialValues.Add(potentialIp);
                }
            }

            var result = IpExtractor.GetRealIpFromValues(ipPotentialValues, isSecureConnection);

            if (string.IsNullOrEmpty(result.IpAddress))
            {
                result = peerIpFallback;
            }

            return new ExtractedHeadersAndIpInfos(headersDic, result);
        }
    }
}
