using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Helper class that tries to provide unique ports numbers across processes and threads in the same machine.
    /// Used avoid port conflicts in concurrent tests that use the Agent, IIS, HttpListener, HttpClient, etc.
    /// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
    /// </summary>
    public static class TcpPortProvider
    {
        private static readonly ConcurrentBag<int> ReturnedPorts = new ConcurrentBag<int>();

        // multiply by 100 so each process gets a built-in 100-port buffer
        private static readonly int MinPort = 10000 + ((Process.GetCurrentProcess().Id * 100) % 10000);

        public static int GetOpenPort(int minPort = 0, int maxPort = ushort.MaxValue)
        {
            var usedPorts = GetUsedPorts();

            for (int port = Math.Max(MinPort, minPort); port < maxPort; port++)
            {
                if (!ReturnedPorts.Contains(port) && !usedPorts.Contains(port))
                {
                    // don't return a port that was previously returned,
                    // even if it is not in use (if could still be used
                    // by the code that is was returned to)
                    ReturnedPorts.Add(port);
                    return port;
                }
            }

            throw new Exception("No open TCP port found.");
        }

        public static List<int> GetUsedPorts()
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                                     .GetActiveTcpListeners()
                                     .Select(ipEndPoint => ipEndPoint.Port)
                                     .OrderBy(p => p)
                                     .ToList();
        }
    }
}
