// <copyright file="UdsClientRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports
{
    internal class UdsClientRequestFactory : IApiRequestFactory
    {
        private readonly HttpClient _client;
        private readonly UnixDomainSocketEndPoint _endPoint;

        public UdsClientRequestFactory(KeyValuePair<string, string>[] defaultHeaders, string socketPath, TimeSpan? timeout = null)
        {
            _endPoint = new UnixDomainSocketEndPoint(socketPath);
            _client = new HttpClient(new SocketsHttpHandler
            {
                ConnectCallback = async (_, token) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    try
                    {
                        await socket.ConnectAsync(_endPoint, token).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });

            if (timeout.HasValue)
            {
                _client.Timeout = timeout.Value;
            }

            foreach (var pair in defaultHeaders)
            {
                _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }
        }

        public string Info(Uri endpoint)
        {
            return endpoint.ToString();
        }

        public Uri GetEndpoint(string relativePath)
        {
            // HttpClient requires a "valid" host header, and will only accept http:// or https:// schemes
            // The host part of the endpoint is irrelevant, as we're using the UDS socket
            // See also HttpStreamRequestFactory
            return UriHelpers.Combine(new Uri("http://localhost"), relativePath);
        }

        public IApiRequest Create(Uri endpoint)
        {
            return new HttpClientRequest(_client, endpoint);
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
        }
    }
}
#endif
