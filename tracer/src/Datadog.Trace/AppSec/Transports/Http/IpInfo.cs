// <copyright file="IpInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class IpInfo
    {
        public IpInfo(string ipAddress, int port, bool ipv6)
        {
            IpAddress = ipAddress;
            Port = port;
            Ipv6 = ipv6;
        }

        public string IpAddress { get; }

        public int Port { get; }

        public bool Ipv6 { get; }

        public override string ToString() => Ipv6 ? $"{IpAddress}:{Port}" : $"[{IpAddress}]:{Port}";
    }
}
