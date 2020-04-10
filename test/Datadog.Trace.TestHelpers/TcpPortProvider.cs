using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Helper class that tries to provide unique ports numbers across processes and threads in the same machine.
    /// Used avoid port conflicts in concurrent tests that use the Agent, IIS, HttpListener, HttpClient, etc.
    /// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
    /// </summary>
    public static class TcpPortProvider
    {
        private static readonly object PortLock = new { };
        private static readonly ConcurrentBag<int> ReturnedPorts = new ConcurrentBag<int>();
        [ThreadStatic]
        private static readonly int MinPort = GetStartingPort();

        public static int GetOpenPort()
        {
            lock (PortLock)
            {
                var usedPorts = GetUsedPorts();

                for (int port = MinPort; port < ushort.MaxValue; port++)
                {
                    if (!ReturnedPorts.Contains(port) && !usedPorts.Contains(port))
                    {
                        // don't return a port that was previously returned,
                        // even if it is not in use (it could still be used
                        // by the code that is was returned to)
                        ReturnedPorts.Add(port);
                        return port;
                    }
                }
            }

            throw new Exception("No open TCP port found.");
        }

        public static HashSet<int> GetUsedPorts()
        {
            var usedPorts = IPGlobalProperties.GetIPGlobalProperties()
                                              .GetActiveTcpListeners()
                                              .Select(ipEndPoint => ipEndPoint.Port);
            usedPorts.Concat(IPGlobalProperties.GetIPGlobalProperties()
                                               .GetActiveTcpConnections()
                                               .Select(information => information.LocalEndPoint.Port));

            return new HashSet<int>(usedPorts);
        }

        private static int GetStartingPort()
        {
            // pick a starting port from the ephemeral port range (49152 – 65535),
            // use process and threads ids to try to get different ports for each thread.
            // https://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
            const int startPort = 49152;
            const int endPort = 65535;
            const int poolSize = endPort - startPort;
            int hash = 17;

            unchecked
            {
                hash = (hash * 23) + Process.GetCurrentProcess().Id.GetHashCode();
                hash = (hash * 23) + Thread.CurrentThread.ManagedThreadId.GetHashCode();
            }

            int offset = hash % poolSize;
            return startPort + offset;
        }
    }
}
