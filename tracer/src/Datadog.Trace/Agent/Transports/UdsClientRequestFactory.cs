// <copyright file="UdsClientRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.Net.Http;
using System.Net.Sockets;

namespace Datadog.Trace.Agent.Transports
{
    internal class UdsClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;

        public UdsClientRequestFactory(string socketPath)
        {
            _client = new HttpClient(new SocketsHttpHandler
            {
                ConnectCallback = async (context, token) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    var endpoint = new UnixDomainSocketEndPoint(socketPath);
                    await socket.ConnectAsync(endpoint).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            });

            foreach (var pair in AgentHttpHeaderNames.DefaultHeaders)
            {
                _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }
        }

        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }
    }
}
#endif
