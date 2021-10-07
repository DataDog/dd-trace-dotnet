// <copyright file="IpExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Datadog.Trace.AppSec.Transports.Http
{
    internal static class IpExtractor
    {
        private static readonly List<Tuple<int, int>> _ipv4Cidrs;
        private static readonly Regex _ipv6Regex;

        static IpExtractor()
        {
            _ipv4Cidrs = new List<Tuple<int, int>>();

            foreach (var currentCidrMask in new string[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
            {
                var parts = currentCidrMask.Split('/');

                var cidrAddr = BitConverter.ToInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                var cidrMask = IPAddress.HostToNetworkOrder(-1 << (32 - int.Parse(parts[1])));
                _ipv4Cidrs.Add(new Tuple<int, int>(cidrAddr, cidrMask));
            }

            _ipv6Regex = new Regex(@"\[(\S*)\]:(\d*)");
        }

        /// <summary>
        /// Can be a list of single or comma separated values ips like [ "192.68.12.1", "172.53.22.11, 181.92.91.1, 193.92.91.1".. ]
        /// </summary>
        /// <param name="headerValues">all the extracted values from ip related headers</param>
        /// <param name="defaultPort">default port if not in the values</param>
        /// <returns>return ip and port</returns>
        internal static IpInfo GetRealIpFromValues(IEnumerable<string> headerValues, int defaultPort)
        {
            var privateIp = string.Empty;
            var remotePort = defaultPort;
            foreach (var header in headerValues)
            {
                var values = header.Split(',');
                foreach (var potentialIp in values)
                {
                    var consideredPotentialIp = potentialIp.Trim();
                    if (string.IsNullOrEmpty(consideredPotentialIp))
                    {
                        continue;
                    }

                    var addressAndPort = ExtractAddressAndPort(consideredPotentialIp, remotePort);
                    consideredPotentialIp = addressAndPort.IpAddress;
                    remotePort = addressAndPort.Port;

                    var success = IPAddress.TryParse(consideredPotentialIp, out var ipAddress);
                    if (success)
                    {
                        if (ipAddress.IsIPv4MappedToIPv6)
                        {
                            ipAddress = ipAddress.MapToIPv4();
                        }

                        if (ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6LinkLocal || IsIpInRange(ipAddress, _ipv4Cidrs))
                        {
                            privateIp = ipAddress.ToString();
                        }
                        else
                        {
                            return new IpInfo(ipAddress.ToString(), remotePort);
                        }
                    }
                }
            }

            return new IpInfo(privateIp, remotePort);
        }

        internal static IpInfo ExtractAddressAndPort(string ip, int defaultPort)
        {
            var port = defaultPort;
            var parts = ip.Split(':');
            if (parts.Length == 2)
            {
                int.TryParse(parts.Last(), out port);
                return new IpInfo(parts[0], port);
            }

            var result = _ipv6Regex.Match(ip);
            if (result.Success)
            {
                ip = result.Groups[1].Captures[0].Value;
                int.TryParse(result.Groups[2].Captures[0].Value, out port);
            }

            return new IpInfo(ip, port);
        }

        internal static bool IsIpInRange(IPAddress ipAdd, IEnumerable<Tuple<int, int>> cidrs)
        {
            var ipAddr = BitConverter.ToInt32(ipAdd.GetAddressBytes(), 0);
            foreach (var currentCidrMask in cidrs)
            {
                var result = (ipAddr & currentCidrMask.Item2) == (currentCidrMask.Item1 & currentCidrMask.Item2);
                if (result)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
