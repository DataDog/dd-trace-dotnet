// <copyright file="RequestHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.EventModel;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal static class RequestHeadersHelper
    {
        internal static void FillHeaders(Func<string, string> getHeader, string customIpHeader, string peerIp, Request request)
        {
            var headersDic = new Dictionary<string, string>();
            var ipPotentialValues = new List<string>();

            foreach (var headerToSend in HeadersConstants.OtherHeaders)
            {
                headersDic.Add(headerToSend, getHeader(headerToSend));
            }

            if (!string.IsNullOrEmpty(customIpHeader))
            {
                var value = getHeader(customIpHeader);
                if (!string.IsNullOrEmpty(value))
                {
                    ipPotentialValues.Add(getHeader(customIpHeader));
                }
            }

            foreach (var headerIp in HeadersConstants.IpHeaders)
            {
                var potentialIp = getHeader(headerIp);
                headersDic.Add(headerIp, potentialIp);
                if (!string.IsNullOrEmpty(potentialIp))
                {
                    ipPotentialValues.Add(potentialIp);
                }
            }

            var ip = IpExtractor.GetRealIpFromValues(ipPotentialValues);

            if (string.IsNullOrEmpty(ip.Item1))
            {
                ip = IpExtractor.ExtractAddressAndPort(peerIp);
            }

            request.RemoteIp = ip.Item1;
            request.RemotePort = ip.Item2;
            request.Headers = headersDic;
        }
    }
}
