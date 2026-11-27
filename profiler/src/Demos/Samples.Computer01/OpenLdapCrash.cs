// <copyright file="OpenLdapCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Threading;

namespace Samples.Computer01
{
    internal class OpenLdapCrash : ScenarioBase
    {
        private readonly string _serverHostname;
        private readonly int _serverPort;

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

            // Throttle to <= 200 connections/s (<= 12,000 TIME_WAIT sockets in Linux's 60 s sliding window,
            // well below the default 28,232 ephemeral ports in 32768-60999) regardless of how many frameworks
            // run sequentially in the same container.
            Thread.Sleep(5);
        }

        // Intentionally creates and disposes a new LdapConnection on every iteration (do NOT reuse one
        // connection across iterations): this test exercises the connect() syscall wrapper in
        // Datadog.Linux.ApiWrapper/socket_operations.c in addition to poll/select/recv.
        //
        // Because the client closes each TCP connection first, the test container's Linux kernel keeps each
        // (client_ip, ephemeral_port, openldap_ip, 389) 4-tuple in TIME_WAIT for 60 s, and TIME_WAIT entries
        // outlive the child process. If the client container runs out of ephemeral ports, Linux fails
        // connect() locally with EADDRNOTAVAIL (errno 99, "Address not available") without contacting the
        // server, and libldap surfaces that as the misleading "LdapException: The LDAP server is unavailable."
        // even though the LDAP server is healthy.
        //
        // If this error ever recurs (e.g. if OpenLdapTests runs in parallel or ip_local_port_range shrinks),
        // increase the sleep in OnProcess or set sysctls `net.ipv4.tcp_tw_reuse: 1` on the
        // ProfilerIntegrationTests service in docker-compose.yml (1, not 2, because openldap-server is a
        // separate container, not loopback).
        private void ConnectToLdapServer()
        {
            try
            {
                using var ldapConnection = new LdapConnection(new LdapDirectoryIdentifier(_serverHostname, _serverPort), new NetworkCredential("cn=admin,dc=dd-trace-dotnet,dc=com", "Passw0rd"), AuthType.Basic);
                ldapConnection.SessionOptions.ProtocolVersion = 3;
                ldapConnection.Bind();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error] An error occurred while trying to connect to the LDAP server `{_serverHostname}:{_serverPort}`. " + e.ToString());
            }
        }
    }
}
#endif
