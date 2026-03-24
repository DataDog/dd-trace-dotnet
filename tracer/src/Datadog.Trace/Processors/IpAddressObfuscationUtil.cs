// <copyright file="IpAddressObfuscationUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    /// <summary>
    /// Quantizes (obfuscates) IP addresses in peer tags to reduce cardinality.
    /// Based on https://github.com/DataDog/datadog-agent/blob/main/pkg/obfuscate/ip_address.go
    /// </summary>
    internal static class IpAddressObfuscationUtil
    {
        private const string BlockedIpAddress = "blocked-ip-address";

        private static readonly HashSet<string> AllowedIpAddresses = new(
            [
                "127.0.0.1", // localhost
                "::1", // IPv6 localhost
                "169.254.169.254", // link-local cloud metadata
                "fd00:ec2::254", // EC2 IPv6 metadata
                "169.254.170.2", // ECS task metadata
            ],
            StringComparer.Ordinal);

        private static readonly string[] Schemes = ["dnspoll://", "ftp://", "file://", "http://", "https://"];

        /// <summary>
        /// Quantizes IP addresses in a comma-separated list of peer addresses.
        /// Preserves hostnames and allowed IPs, replaces others with "blocked-ip-address".
        /// Deduplicates entries after quantization.
        /// </summary>
        public static string QuantizePeerIpAddresses(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            // Fast path: no commas, single entry
            if (raw.IndexOf(',') < 0)
            {
                return QuantizeIp(raw);
            }

            // we don't expect many instances so just use a list instead of a HashSet
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sb = StringBuilderCache.Acquire(raw.Length);

            var start = 0;
            while (start <= raw.Length)
            {
                var commaIndex = raw.IndexOf(',', start);
                if (commaIndex < 0)
                {
                    commaIndex = raw.Length;
                }

                var entry = raw.Substring(start, commaIndex - start);
                start = commaIndex + 1;

                var quantized = QuantizeIp(entry);
                if (seen.Add(quantized))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(quantized);
                }
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static string QuantizeIp(string raw)
        {
            if (raw.Length == 0)
            {
                return raw;
            }

            var prefix = SplitPrefix(raw, out var after);

            ParseIpAndPort(after, out var host, out var port, out var suffix);

            if (AllowedIpAddresses.Contains(host))
            {
                return raw;
            }

            if (!IsParseableIp(host))
            {
                return raw;
            }

            // Reconstruct: prefix + blocked + suffix + port
            var sb = StringBuilderCache.Acquire(raw.Length);
            sb.Append(prefix);
            sb.Append(BlockedIpAddress);
            sb.Append(suffix);
            if (port.Length > 0)
            {
                sb.Append(':');
                sb.Append(port);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Extracts a URL scheme prefix (e.g. "http://", "dnspoll:///") or "ip-" prefix.
        /// Returns the prefix and sets <paramref name="after"/> to the remainder.
        /// </summary>
        private static string SplitPrefix(string raw, out string after)
        {
            // Check for scheme prefixes
            foreach (var scheme in Schemes)
            {
                if (raw.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    var idx = scheme.Length;
                    // consume any additional slashes (e.g. "dnspoll:///")
                    while (idx < raw.Length && raw[idx] == '/')
                    {
                        idx++;
                    }

                    after = raw.Substring(idx);
                    return raw.Substring(0, idx);
                }
            }

            // Check for "ip-" prefix (AWS EC2 hostnames)
            if (raw.Length > 3 && raw[0] == 'i' && raw[1] == 'p' && raw[2] == '-')
            {
                after = raw.Substring(3);
                return "ip-";
            }

            after = raw;
            return string.Empty;
        }

        /// <summary>
        /// Splits input into host, port, and suffix.
        /// Handles bracketed IPv6 ([host]:port), IPv4:port, and alternate-separator IPs with suffixes.
        /// </summary>
        private static void ParseIpAndPort(string input, out string host, out string port, out string suffix)
        {
            suffix = string.Empty;

            // Bracketed IPv6: [host]:port
            if (input.Length > 0 && input[0] == '[')
            {
                var closeBracket = input.IndexOf(']');
                if (closeBracket > 0)
                {
                    host = input.Substring(1, closeBracket - 1);
                    if (closeBracket + 1 < input.Length && input[closeBracket + 1] == ':')
                    {
                        port = input.Substring(closeBracket + 2);
                    }
                    else
                    {
                        port = string.Empty;
                    }

                    return;
                }
            }

            // Try standard dot-separated IPv4 first
            if (ParseIPv4(input, '.', out var lastDotIndex))
            {
                host = input.Substring(0, lastDotIndex);
                suffix = input.Substring(lastDotIndex); // includes any trailing ".foo.bar"
                // Check for port on the full host (host might be "1.2.3.4:port")
                // Actually, with dot separator, port comes after the IP: "1.2.3.4:1234"
                SplitHostPort(input, out host, out port, out suffix);
                return;
            }

            // Try underscore-separated IPv4
            if (ParseIPv4(input, '_', out var lastUnderscoreIndex))
            {
                host = input.Substring(0, lastUnderscoreIndex);
                suffix = input.Substring(lastUnderscoreIndex);
                port = string.Empty;
                return;
            }

            // Try hyphen-separated IPv4
            if (ParseIPv4(input, '-', out var lastHyphenIndex))
            {
                host = input.Substring(0, lastHyphenIndex);
                suffix = input.Substring(lastHyphenIndex);
                port = string.Empty;
                return;
            }

            // Try IPv6 (without brackets)
            if (IPAddress.TryParse(input, out var addr) && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                host = input;
                port = string.Empty;
                return;
            }

            // Try host:port for non-alternate-separator cases
            var colonIdx = input.LastIndexOf(':');
            if (colonIdx > 0)
            {
                var potentialHost = input.Substring(0, colonIdx);
                var potentialPort = input.Substring(colonIdx + 1);

                // Only treat as port if the part after colon looks like a port number
                if (IsNumeric(potentialPort))
                {
                    host = potentialHost;
                    port = potentialPort;
                    return;
                }
            }

            host = input;
            port = string.Empty;
        }

        /// <summary>
        /// Splits a dot-separated input into host (the IP part), port, and suffix.
        /// For "192.168.1.1:1234", host="192.168.1.1", port="1234", suffix="".
        /// For "192.168.1.1.foo", host="192.168.1.1", port="", suffix=".foo".
        /// </summary>
        private static void SplitHostPort(string input, out string host, out string port, out string suffix)
        {
            // First, check if there's a colon for port
            var colonIdx = input.IndexOf(':');
            string withoutPort;
            if (colonIdx > 0)
            {
                withoutPort = input.Substring(0, colonIdx);
                port = input.Substring(colonIdx + 1);
            }
            else
            {
                withoutPort = input;
                port = string.Empty;
            }

            // Now find where the IP ends in the dotted string
            // Parse up to 4 octets
            var octetCount = 0;
            var i = 0;
            var lastIpEnd = 0;
            while (i < withoutPort.Length && octetCount < 4)
            {
                var octetStart = i;
                while (i < withoutPort.Length && withoutPort[i] >= '0' && withoutPort[i] <= '9')
                {
                    i++;
                }

                if (i == octetStart)
                {
                    break; // no digit found
                }

                var octetLen = i - octetStart;
                if (octetLen > 3)
                {
                    break;
                }

                var val = ParseOctetValue(withoutPort, octetStart, octetLen);
                if (val > 255)
                {
                    break;
                }

                octetCount++;
                lastIpEnd = i;

                if (octetCount < 4 && i < withoutPort.Length && withoutPort[i] == '.')
                {
                    i++; // skip dot
                }
                else
                {
                    break;
                }
            }

            if (octetCount == 4)
            {
                host = withoutPort.Substring(0, lastIpEnd);
                suffix = withoutPort.Substring(lastIpEnd);
            }
            else
            {
                host = withoutPort;
                suffix = string.Empty;
            }
        }

        /// <summary>
        /// Checks if the string starts with a valid IP address (IPv4 with any separator, or IPv6).
        /// </summary>
        private static bool IsParseableIp(string s)
        {
            int lastIndex;

            if (s.Length == 0)
            {
                return false;
            }

            // Try dot-separated IPv4
            if (ParseIPv4(s, '.', out lastIndex) && lastIndex == s.Length)
            {
                return true;
            }

            // Try underscore-separated IPv4
            if (ParseIPv4(s, '_', out lastIndex) && lastIndex == s.Length)
            {
                return true;
            }

            // Try hyphen-separated IPv4
            if (ParseIPv4(s, '-', out lastIndex) && lastIndex == s.Length)
            {
                return true;
            }

            // Try IPv6
            if (IPAddress.TryParse(s, out var addr) && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return true;
            }

            return false;
        }

        private static bool ParseIPv4(string s, char sep, out int lastIndex)
        {
            lastIndex = 0;
            var octetCount = 0;
            var i = 0;

            while (i < s.Length && octetCount < 4)
            {
                var octetStart = i;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                {
                    i++;
                }

                var octetLen = i - octetStart;
                if (octetLen == 0 || octetLen > 3)
                {
                    return false;
                }

                var val = ParseOctetValue(s, octetStart, octetLen);
                if (val > 255)
                {
                    return false;
                }

                octetCount++;

                if (octetCount < 4)
                {
                    if (i < s.Length && s[i] == sep)
                    {
                        i++; // skip separator
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (octetCount == 4)
            {
                lastIndex = i;
                return true;
            }

            return false;
        }

        private static int ParseOctetValue(string s, int start, int length)
        {
            var val = 0;
            for (var i = start; i < start + length; i++)
            {
                val = (val * 10) + (s[i] - '0');
            }

            return val;
        }

        private static bool IsNumeric(string s)
        {
            if (s.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
