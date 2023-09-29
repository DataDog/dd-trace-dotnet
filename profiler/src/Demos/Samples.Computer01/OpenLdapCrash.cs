// <copyright file="OpenLdapCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NET5_0_OR_GREATER
using System;
using System.DirectoryServices.Protocols;
using System.Net;

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
        }

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
                Console.WriteLine($"[Error] An error occured while trying to connect to the LDAP server `{_serverHostname}:{_serverPort}`. Message: " + e.Message);
            }
        }
    }
}
#endif
