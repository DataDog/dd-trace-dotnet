// <copyright file="TcpStreamFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Datadog.Trace.Agent.StreamFactories
{
    /// <summary>
    /// Experimental TCP based stream factory for exploring replacing System.Net.Http
    /// </summary>
    internal class TcpStreamFactory : IStreamFactory
    {
        private readonly string _host;
        private readonly int _port;

        public TcpStreamFactory(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public string Info()
        {
            return nameof(TcpStreamFactory);
        }

        public Stream GetBidirectionalStream()
        {
            return ConnectSocket(_host, _port);
        }

        private static NetworkStream ConnectSocket(string host, int port)
        {
            var ipAddress = Dns.GetHostAddresses(host).FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            var endpoint = new IPEndPoint(ipAddress, port);
            var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);
            return new NetworkStream(socket, FileAccess.ReadWrite, ownsSocket: true);
        }
    }
}
