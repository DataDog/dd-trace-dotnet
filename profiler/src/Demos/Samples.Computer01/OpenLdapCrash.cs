// <copyright file="OpenLdapCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Samples.Computer01
{
    internal class OpenLdapCrash : ScenarioBase
    {
        private readonly string _serverHostname;
        private readonly int _serverPort;

        // Diagnostic counters (single worker thread, read after Stop)
        private long _attempts;
        private long _successes;
        private long _failures;
        private long _bindTicks;

        public OpenLdapCrash()
        {
            var uri = Environment.GetEnvironmentVariable("LDAP_SERVER") ?? "localhost:389";

            try
            {
                (_serverHostname, _serverPort) = uri.IndexOf(':') switch
                {
                    var colonIdx when colonIdx > -1 => (uri[..colonIdx], int.Parse(uri[(colonIdx + 1)..])),
                    _ => (uri, 389)
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    $"[Error] Failed to parse ldap server connection information. LDAP_SERVER env var: {Environment.GetEnvironmentVariable("LDAP_SERVER")}" +
                    $" , Uri: {uri}. Error: {e.Message}");
            }

            Console.WriteLine($"[Info] OpenLdapCrash start: {DateTime.UtcNow:O}, {CountTimeWaitSockets()} TIME_WAIT sockets to port {_serverPort}");
        }

        public override void OnProcess()
        {
            if (_serverHostname == null)
            {
                return;
            }

            ConnectToLdapServer();
        }

        public void PrintStatistics()
        {
            var bindMs = _bindTicks * 1000 / Stopwatch.Frequency;
            Console.WriteLine(
                $"[Info] OpenLdapCrash end: {DateTime.UtcNow:O}, attempts={_attempts}, successes={_successes}, failures={_failures}, bindTimeMs={bindMs}");

            string portRange;
            try
            {
                portRange = File.ReadAllText("/proc/sys/net/ipv4/ip_local_port_range").Trim();
            }
            catch (Exception e)
            {
                portRange = $"unknown ({e.Message})";
            }

            Console.WriteLine($"[Info] OpenLdapCrash TIME_WAIT sockets to port {_serverPort}: {CountTimeWaitSockets()}, ip_local_port_range: {portRange}");
        }

        // Each call opens (and closes) a new TCP connection and OnProcess calls it in a tight loop.
        // The client side keeps the sockets in TIME_WAIT for 60s, so running this scenario several times
        // in a row in the same network namespace can exhaust the ephemeral ports: connect then fails and
        // Bind throws 'The LDAP server is unavailable'.
        // If this happens, add a small sleep after Bind (e.g. Thread.Sleep(5)) to reduce the number of
        // connections, or enable net.ipv4.tcp_tw_reuse in the container running the test.
        private void ConnectToLdapServer()
        {
            _attempts++;
            var start = Stopwatch.GetTimestamp();
            try
            {
                using var ldapConnection = new LdapConnection(new LdapDirectoryIdentifier(_serverHostname, _serverPort), new NetworkCredential("cn=admin,dc=dd-trace-dotnet,dc=com", "Passw0rd"), AuthType.Basic);
                ldapConnection.SessionOptions.ProtocolVersion = 3;
                ldapConnection.Bind();
                _bindTicks += Stopwatch.GetTimestamp() - start;
                _successes++;
            }
            catch (Exception e)
            {
                _bindTicks += Stopwatch.GetTimestamp() - start;
                _failures++;
                Console.WriteLine($"[Error] An error occurred while trying to connect to the LDAP server `{_serverHostname}:{_serverPort}`. " + e.ToString());

                if (_failures == 1)
                {
                    ProbeSocketConnect();
                }
            }
        }

        private void ProbeSocketConnect()
        {
            var info = $"[Info] OpenLdapCrash first failure at attempt {_attempts} ({DateTime.UtcNow:O}): {CountTimeWaitSockets()} TIME_WAIT sockets to port {_serverPort}, Socket.Connect";
            try
            {
                using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(_serverHostname, _serverPort);
                Console.WriteLine($"{info} succeeded");
            }
            catch (SocketException e)
            {
                Console.WriteLine($"{info} failed: SocketError={e.SocketErrorCode}, NativeErrorCode={e.NativeErrorCode}, Message={e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{info} failed: {e.GetType().Name}: {e.Message}");
            }
        }

        // Counts the sockets in TIME_WAIT (state 06) to the LDAP server port in /proc/net/tcp and /proc/net/tcp6.
        private int CountTimeWaitSockets()
        {
            var count = 0;
            foreach (var file in new[] { "/proc/net/tcp", "/proc/net/tcp6" })
            {
                try
                {
                    foreach (var line in File.ReadLines(file).Skip(1))
                    {
                        var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (columns.Length < 4 || columns[3] != "06")
                        {
                            continue;
                        }

                        var remote = columns[2];
                        var port = Convert.ToInt32(remote[(remote.LastIndexOf(':') + 1)..], 16);
                        if (port == _serverPort)
                        {
                            count++;
                        }
                    }
                }
                catch (Exception)
                {
                    return -1;
                }
            }

            return count;
        }
    }
}
#endif
