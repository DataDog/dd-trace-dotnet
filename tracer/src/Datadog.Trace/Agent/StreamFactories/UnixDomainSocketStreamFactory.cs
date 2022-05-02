// <copyright file="UnixDomainSocketStreamFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP3_1_OR_GREATER

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.StreamFactories
{
    internal class UnixDomainSocketStreamFactory : IStreamFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UnixDomainSocketStreamFactory));
        private readonly UnixDomainSocketEndPoint _endPoint;
        private readonly string _path;

        public UnixDomainSocketStreamFactory(string serverName)
        {
            _path = serverName.Replace(ExporterSettings.UnixDomainSocketPrefix, string.Empty);
            _endPoint = new UnixDomainSocketEndPoint(_path);
        }

        public string Info()
        {
            return _path;
        }

        public Stream GetBidirectionalStream()
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                socket.Connect(_endPoint);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "There was a problem connecting to the socket");
                socket?.Dispose();
                throw;
            }
        }

#if NET5_0_OR_GREATER
        public async Task<Stream> GetBidirectionalStreamAsync(CancellationToken token)
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                await socket.ConnectAsync(_endPoint, token).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "There was a problem connecting to the socket");
                socket?.Dispose();
                throw;
            }
        }
#endif
    }
}
#endif
