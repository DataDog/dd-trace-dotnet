// <copyright file="OpenLdapCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
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
        private long _attempts;
        private long _failures;

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
        }

        public override void OnProcess()
        {
            if (_serverHostname == null)
            {
                return;
            }

            ConnectToLdapServer();
        }

        private static string ReadProcFile(string path)
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch (Exception e)
            {
                return $"unknown ({e.Message})";
            }
        }

        // Intentionally creates and disposes a new LdapConnection on every iteration (do NOT reuse one
        // connection across iterations): this test exercises the connect() syscall wrapper in
        // Datadog.Linux.ApiWrapper/socket_operations.c in addition to poll/select/recv.
        //
        // Because the client closes each TCP connection first, the test container's Linux kernel keeps each
        // (client_ip, ephemeral_port, openldap_ip, 389) 4-tuple in TIME_WAIT for 60 s, and TIME_WAIT entries
        // outlive the child process. If the client runs out of ephemeral ports, Linux fails connect() locally
        // with EADDRNOTAVAIL (errno 99, "Address not available") without contacting the server, and libldap
        // surfaces that as the misleading "LdapException: The LDAP server is unavailable." even though the
        // LDAP server is healthy.
        //
        // In docker-compose.yml, ProfilerIntegrationTests sets `net.ipv4.tcp_tw_reuse=1` so the container can
        // reuse TIME_WAIT port numbers for new connections after 1 s instead of 60 s (keeping full connect()
        // coverage without throttling). Note: if you run all frameworks outside docker-compose against a
        // non-loopback LDAP server IP, Linux's default `net.ipv4.tcp_tw_reuse=2` only applies to loopback
        // (localhost), so enable `net.ipv4.tcp_tw_reuse=1` there too (or target localhost).
        private void ConnectToLdapServer()
        {
            _attempts++;
            try
            {
                using var ldapConnection = new LdapConnection(new LdapDirectoryIdentifier(_serverHostname, _serverPort), new NetworkCredential("cn=admin,dc=dd-trace-dotnet,dc=com", "Passw0rd"), AuthType.Basic);
                ldapConnection.SessionOptions.ProtocolVersion = 3;
                ldapConnection.Bind();
            }
            catch (Exception e)
            {
                _failures++;
                Console.WriteLine($"[Error] An error occurred while trying to connect to the LDAP server `{_serverHostname}:{_serverPort}`. " + e.ToString());

                if (_failures == 1)
                {
                    LogFirstFailureDiagnostics();
                }
            }
        }

        // Only runs on the first Bind failure (zero cost on passing runs). libldap collapses almost any
        // socket failure into "The LDAP server is unavailable", so this distinguishes client-side port
        // exhaustion (EADDRNOTAVAIL + high TIME_WAIT) from an unreachable server or a post-connect error.
        private void LogFirstFailureDiagnostics()
        {
            var timeWaitCount = CountTimeWaitSockets();
            var portRange = ReadProcFile("/proc/sys/net/ipv4/ip_local_port_range");
            var twReuse = ReadProcFile("/proc/sys/net/ipv4/tcp_tw_reuse");
            var prefix = $"[Info] OpenLdapCrash first failure at attempt {_attempts} ({DateTime.UtcNow:O}): {timeWaitCount} TIME_WAIT sockets to port {_serverPort}, ip_local_port_range={portRange}, tcp_tw_reuse={twReuse}, Socket.Connect";

            try
            {
                using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(_serverHostname, _serverPort);
                Console.WriteLine($"{prefix} succeeded");
            }
            catch (SocketException e)
            {
                Console.WriteLine($"{prefix} failed: SocketError={e.SocketErrorCode}, NativeErrorCode={e.NativeErrorCode}, Message={e.Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{prefix} failed: {e.GetType().Name}: {e.Message}");
            }
        }

        // Counts sockets in TIME_WAIT (state 06) to the LDAP server port in /proc/net/tcp and /proc/net/tcp6.
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
