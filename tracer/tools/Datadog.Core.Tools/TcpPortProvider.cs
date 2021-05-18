// <copyright file="TcpPortProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;
using System.Net.Sockets;

namespace Datadog.Core.Tools
{
    /// <summary>
    /// Helper class that tries to provide unique ports numbers across processes and threads in the same machine.
    /// Used avoid port conflicts in concurrent tests that use the Agent, IIS, HttpListener, HttpClient, etc.
    /// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
    /// </summary>
    public static class TcpPortProvider
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
