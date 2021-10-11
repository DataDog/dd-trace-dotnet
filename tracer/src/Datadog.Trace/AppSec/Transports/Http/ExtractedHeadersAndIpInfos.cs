// <copyright file="ExtractedHeadersAndIpInfos.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class ExtractedHeadersAndIpInfos
    {
        public ExtractedHeadersAndIpInfos(IDictionary<string, string> headersToSend, string address, int port)
        {
            HeadersToSend = headersToSend;
            IpInfo = new IpInfo(address, port);
        }

        public ExtractedHeadersAndIpInfos(IDictionary<string, string> headersToSend, IpInfo ipInfo)
        {
            HeadersToSend = headersToSend;
            IpInfo = ipInfo;
        }

        public IDictionary<string, string> HeadersToSend { get; }

        public IpInfo IpInfo { get; }
    }
}
