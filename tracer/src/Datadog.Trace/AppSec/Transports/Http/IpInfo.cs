// <copyright file="IpInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal class IpInfo
    {
        public IpInfo(string ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;
        }

        public string IpAddress { get; }

        public int Port { get; }
    }
}
