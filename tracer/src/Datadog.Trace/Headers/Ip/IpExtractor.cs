// <copyright file="IpExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Datadog.Trace.Headers.Ip
{
    internal static class IpExtractor
    {
        private static readonly List<Tuple<int, int>> _ipv4LocalCidrs;
        private static readonly Regex _ipv6Regex;

        static IpExtractor()
        {
            _ipv4LocalCidrs = new List<Tuple<int, int>>();

            foreach (var currentCidrMask in new[] { "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16" })
            {
                var parts = currentCidrMask.Split('/');

                var cidrAddr = BitConverter.ToInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                var cidrMask = IPAddress.HostToNetworkOrder(-1 << (32 - int.Parse(parts[1])));
                _ipv4LocalCidrs.Add(new Tuple<int, int>(cidrAddr, cidrMask));
            }

            _ipv6Regex = new Regex(@"\[(\S*)\]:(\d*)");
        }

        internal static int DefaultPort(bool https) => https ? 443 : 80;

        /// <summary>
        /// Can be a list of single or comma separated values ips like [ "192.68.12.1", "172.53.22.11, 181.92.91.1, 193.92.91.1".. ]
        /// </summary>
        /// <param name="headerValue">the extracted values from releveant ip related header</param>
        /// <param name="https">is a secure connection</param>
        /// <returns>return ip and port, may be null</returns>
        internal static IpInfo RealIpFromValue(string headerValue, bool https)
        {
            IpInfo privateIpInfo = null;
            var values = headerValue.Split(',');
            foreach (var potentialIp in values)
            {
                var consideredPotentialIp = potentialIp.Trim();
                if (string.IsNullOrEmpty(consideredPotentialIp))
                {
                    continue;
                }

                var addressAndPort = ExtractAddressAndPort(consideredPotentialIp, defaultPort: DefaultPort(https));
                consideredPotentialIp = addressAndPort.IpAddress;

                var success = IPAddress.TryParse(consideredPotentialIp, out var ipAddress);
                if (success)
                {
                    if (ipAddress.IsIPv4MappedToIPv6)
                    {
                        ipAddress = ipAddress.MapToIPv4();
                    }

                    if (IsPrivateIp(ipAddress))
                    {
                        // only set the oldest (so the first one)
                        privateIpInfo ??= addressAndPort;
                    }
                    else
                    {
                        var publicIpInfo = new IpInfo(ipAddress.ToString(), addressAndPort.Port);
                        return publicIpInfo;
                    }
                }
            }

            return privateIpInfo;
        }

        internal static IpInfo ExtractAddressAndPort(string ip, bool https = false, int? defaultPort = null)
        {
            var port = defaultPort ?? DefaultPort(https);
            if (ip.Contains("."))
            {
                var parts = ip.Split(':');
                if (parts.Length == 2)
                {
                    _ = int.TryParse(parts[1], out port);
                    return new IpInfo(parts[0], port);
                }
            }

            var result = _ipv6Regex.Match(ip);
            if (result.Success)
            {
                ip = result.Groups[1].Captures[0].Value;
                _ = int.TryParse(result.Groups[2].Captures[0].Value, out port);
            }

            return new IpInfo(ip, port);
        }

        internal static bool IsPrivateIp(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var ipAddr = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);
                foreach (var currentCidrMask in _ipv4LocalCidrs)
                {
                    var result = (ipAddr & currentCidrMask.Item2) == (currentCidrMask.Item1 & currentCidrMask.Item2);
                    if (result)
                    {
                        return true;
                    }
                }
            }

            if (ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6LinkLocal)
            {
                return true;
            }

#if NET6_0_OR_GREATER
            return ipAddress.IsIPv6UniqueLocal;
#else
            var firstWord = ipAddress.ToString().Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0];
            // These days Unique Local Addresses (ULA) are used in place of Site Local. ULA has two variants:
            // fc00::/8 is not defined yet, but might be used in the future for internal-use addresses
            // fd00::/8 is in use and does not have to registered anywhere.
            if (firstWord.Length >= 4)
            {
                if (firstWord[0] == 'f')
                {
                    return firstWord[1] == 'd' || firstWord[1] == 'c';
                }
            }

            return false;
#endif
        }
    }
}
