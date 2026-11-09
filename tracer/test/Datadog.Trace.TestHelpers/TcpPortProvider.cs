// <copyright file="TcpPortProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;
using System.Net.Sockets;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Helper class that tries to provide unique ports numbers across processes and threads in the same machine.
    /// Used avoid port conflicts in concurrent tests that use the Agent, IIS, HttpListener, HttpClient, etc.
    /// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
    /// </summary>
    public static class TcpPortProvider
    {
        /// <summary>
        /// Starts an HTTP listener, retrying with a fresh listener and port if a port is taken before binding.
        /// The caller owns the returned listener and must dispose it.
        /// </summary>
        /// <param name="baseUrl">The loopback URL successfully bound by the listener.</param>
        /// <returns>The started listener.</returns>
        public static HttpListener StartHttpListenerOnFreePort(out string baseUrl)
        {
            const int MaxAttempts = 5;
            for (var attempt = 1; ; attempt++)
            {
                baseUrl = $"http://127.0.0.1:{GetOpenPort()}/";
                var listener = new HttpListener();
                listener.Prefixes.Add(baseUrl);
                try
                {
                    listener.Start();
                    return listener;
                }
                catch (HttpListenerException)
                {
                    // A listener cannot be reused after a failed Start().
                    listener.Close();
                    if (attempt == MaxAttempts)
                    {
                        throw;
                    }
                }
            }
        }

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
