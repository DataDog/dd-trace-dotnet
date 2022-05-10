// <copyright file="SocketHandlerRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Datadog.Trace.Agent.Transports
{
    internal class SocketHandlerRequestFactory : HttpClientRequestFactory
    {
        public SocketHandlerRequestFactory(IStreamFactory streamFactory, KeyValuePair<string, string>[] defaultHeaders, Uri baseEndpoint, TimeSpan? timeout = null)
            : base(
                // HttpClient requires a "valid" host header, and will only accept http:// or https:// schemes
                // The host part of the endpoint is irrelevant, as we're using the UDS socket/named pipe
                // See also HttpStreamRequestFactory
                baseEndpoint: baseEndpoint,
                defaultHeaders: defaultHeaders,
                timeout: timeout,
                handler: new SocketsHttpHandler
                {
                    ConnectCallback = async (_, token) => await streamFactory.GetBidirectionalStreamAsync(token).ConfigureAwait(false)
                })
        {
        }
    }
}
#endif
