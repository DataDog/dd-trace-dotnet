// <copyright file="TcpPortProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Net;
using System.Net.Sockets;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class TcpPortProvider
    {
        public static int GetOpenPort()
        {
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, 0);
                tcpListener.Start();
                var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                return port;
            }
            finally
            {
                tcpListener?.Stop();
            }
        }
    }
}
