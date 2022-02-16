// <copyright file="TcpPortProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Helper class that tries to provide unique ports numbers across processes and threads on the same machine.
    /// Used to avoid port conflicts in concurrent tests that use the Agent, IIS, HttpListener, HttpClient, etc.
    /// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
    /// </summary>
    public static class TcpPortProvider
    {
        private static readonly Random _rnd = new Random();

        private static readonly object _locker = new object();

        private static readonly PortRange _portRange = GetPortRange();

        private static readonly HashSet<int> _previouslyReturnedPorts = new HashSet<int>();

        public static int GetOpenPort()
        {
            int retriesMax = 1000;
            lock (_locker)
            {
                var usedPorts = GetUsedPorts();
                int retryCount = 0;
                while (retryCount < retriesMax)
                {
                    retryCount++;
                    int port = _rnd.Next(_portRange.RangeLength - 1) + _portRange.MinPort;

                    if (!_previouslyReturnedPorts.Contains(port) && !usedPorts.Contains(port))
                    {
                        // This method should never return the same port number twice,
                        // to further minimize the chance of a race condition (the caller
                        // may still be planning to use the port we handed over but just
                        // hasn't gotten around to it yet).
                        _previouslyReturnedPorts.Add(port);
                        return port;
                    }
                }
            }

            throw new Exception($"No open TCP port found. Reached {retriesMax} retries");
        }

        private static HashSet<int> GetUsedPorts()
        {
            var usedPorts = IPGlobalProperties.GetIPGlobalProperties()
                                              .GetActiveTcpListeners()
                                              .Select(ipEndPoint => ipEndPoint.Port);

            return new HashSet<int>(usedPorts);
        }

        private static PortRange GetPortRange()
        {
            // Pick an arbitrary segment from the ephemeral port range (49152 – 65535)
            // Use process and threads ids to try and minimize the chance of collision
            const int startPort = 49152;
            const int endPort = 65535;
            const int poolSize = 1000; 
            int potentialPools = (endPort - startPort) / poolSize;
            int selectedPool = (Process.GetCurrentProcess().Id.GetHashCode() + 
                                Thread.CurrentThread.ManagedThreadId.GetHashCode()) 
                               % potentialPools;
            
            return new PortRange() { MinPort = startPort + (poolSize * selectedPool), RangeLength = poolSize };
        }

        private class PortRange
        {
            public int MinPort { get; set; }

            public int RangeLength { get; set; }
        }
    }
}
