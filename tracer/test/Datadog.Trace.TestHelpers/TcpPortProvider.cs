// <copyright file="TcpPortProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
            var listener = StartHttpListener(
                port => new[] { $"http://127.0.0.1:{port}/" },
                out var port);
            baseUrl = $"http://127.0.0.1:{port}/";
            return listener;
        }

        /// <summary>
        /// Starts an HTTP listener with prefixes derived from the bound port, retrying with a fresh listener
        /// and port if binding fails. The caller owns the returned listener and must dispose it.
        /// </summary>
        /// <param name="prefixesForPort">Builds the prefixes to register for a candidate port.</param>
        /// <param name="port">The port successfully bound by the listener.</param>
        /// <param name="initialPort">Optional first port to try before allocating a new one.</param>
        /// <param name="maxAttempts">Maximum bind attempts before rethrowing.</param>
        /// <param name="keepInitialPort">When true, every retry rebinds the requested port instead of choosing a new one.</param>
        /// <returns>The started listener.</returns>
        public static HttpListener StartHttpListener(
            Func<int, IEnumerable<string>> prefixesForPort,
            out int port,
            int? initialPort = null,
            int maxAttempts = 5,
            bool keepInitialPort = false)
        {
            for (var attempt = 1; ; attempt++)
            {
                port = (keepInitialPort || attempt == 1) && initialPort.HasValue ? initialPort.Value : GetOpenPort();
                var listener = new HttpListener();
                foreach (var prefix in prefixesForPort(port))
                {
                    listener.Prefixes.Add(prefix);
                }

                try
                {
                    listener.Start();
                    return listener;
                }
                catch (Exception)
                {
                    // A listener cannot be reused after a failed Start().
                    // HttpListener.Dispose() is explicit on .NET Framework.
                    ((IDisposable)listener).Dispose();
                    if (attempt >= maxAttempts)
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
