// <copyright file="IpAddressObfuscationUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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

            // Fast path: no commas, single entry — can return original string if unchanged
            if (raw.IndexOf(',') < 0)
            {
                return QuantizeIpSingle(raw);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var sb = StringBuilderCache.Acquire(raw.Length);
            var remaining = raw.AsSpan();

            while (remaining.Length > 0)
            {
                // Get the next entry
                ReadOnlySpan<char> entry;
                var commaIdx = remaining.IndexOf(',');
                if (commaIdx < 0)
                {
                    entry = remaining;
                    remaining = default;
                }
                else
                {
                    entry = remaining.Slice(0, commaIdx);
                    remaining = remaining.Slice(commaIdx + 1);
                }

                var quantized = QuantizeIpToString(entry);
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

        /// <summary>
        /// Quantizes a single IP entry. Returns the original string if no change is needed (zero-alloc fast path).
        /// </summary>
        private static string QuantizeIpSingle(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            var span = raw.AsSpan();
            var prefixLen = GetPrefixLength(span);
            var after = span.Slice(prefixLen);

            if (!ParseIpAndPort(after, out var hostEnd, out var suffixEnd, out var portStart, out var portEnd))
            {
                return raw;
            }

            var host = after.Slice(0, hostEnd);

            if (IsAllowedIp(host))
            {
                return raw;
            }

            return BuildBlockedResult(span.Slice(0, prefixLen), after.Slice(hostEnd, suffixEnd - hostEnd), after.Slice(portStart, portEnd - portStart));
        }

        /// <summary>
        /// Quantizes a single IP entry from a span. Always allocates a string result (used in multi-entry path).
        /// </summary>
        private static string QuantizeIpToString(ReadOnlySpan<char> raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            var prefixLen = GetPrefixLength(raw);
            var after = raw.Slice(prefixLen);

            if (!ParseIpAndPort(after, out var hostEnd, out var suffixEnd, out var portStart, out var portEnd))
            {
                return raw.ToString();
            }

            var host = after.Slice(0, hostEnd);

            if (IsAllowedIp(host))
            {
                return raw.ToString();
            }

            return BuildBlockedResult(raw.Slice(0, prefixLen), after.Slice(hostEnd, suffixEnd - hostEnd), after.Slice(portStart, portEnd - portStart));
        }

        private static string BuildBlockedResult(ReadOnlySpan<char> prefix, ReadOnlySpan<char> suffix, ReadOnlySpan<char> port)
        {
            var sb = StringBuilderCache.Acquire(prefix.Length + BlockedIpAddress.Length + suffix.Length + port.Length + 1);
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
        /// Returns the length of a URL scheme prefix (e.g. "http://", "dnspoll:///") or "ip-" prefix.
        /// </summary>
        private static int GetPrefixLength(ReadOnlySpan<char> raw)
        {
            // All schemes contain "://" so check for that first as a fast reject
            var colonIndex = raw.IndexOf(':');
            if (colonIndex > 0 && colonIndex + 2 < raw.Length && raw[colonIndex + 1] == '/' && raw[colonIndex + 2] == '/')
            {
                if (MatchesScheme(raw, "dnspoll") ||
                    MatchesScheme(raw, "ftp") ||
                    MatchesScheme(raw, "file") ||
                    MatchesScheme(raw, "http") ||
                    MatchesScheme(raw, "https"))
                {
                    // past "://"
                    var index = colonIndex + 3;
                    // consume any additional slashes (e.g. "dnspoll:///")
                    while (index < raw.Length && raw[index] == '/')
                    {
                        index++;
                    }

                    return index;
                }
            }

            // Check for "ip-" prefix (AWS EC2 hostnames)
            if (raw.Length > 3 && raw[0] == 'i' && raw[1] == 'p' && raw[2] == '-')
            {
                return 3;
            }

            return 0;
        }

        private static bool MatchesScheme(ReadOnlySpan<char> raw, string scheme)
        {
            // scheme + "://"
            if (raw.Length < scheme.Length + 3)
            {
                return false;
            }

            for (var i = 0; i < scheme.Length; i++)
            {
                if ((raw[i] | 0x20) != scheme[i])
                {
                    return false;
                }
            }

            return raw[scheme.Length] == ':' && raw[scheme.Length + 1] == '/' && raw[scheme.Length + 2] == '/';
        }

        /// <summary>
        /// Parses input into host, suffix, and port regions AND determines whether the host is an IP address.
        /// Returns true if the host is a recognized IP address, false if it's a hostname or other non-IP string.
        /// All output indices are relative to <paramref name="input"/>:
        /// host=[0..hostEnd), suffix=[hostEnd..suffixEnd), port=[portStart..portEnd).
        /// </summary>
        private static bool ParseIpAndPort(ReadOnlySpan<char> input, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
        {
            // Bracketed IPv6: [host]:port — brackets definitively indicate IPv6
            if (input.Length > 0 && input[0] == '[')
            {
                var closeBracket = input.IndexOf(']');
                if (closeBracket > 1)
                {
                    // Validate the content between brackets is actually IPv6
                    var inner = input.Slice(1, closeBracket - 1);
                    if (!IsIPv6(inner))
                    {
                        return SetDefault(input.Length, out hostEnd, out suffixEnd, out portStart, out portEnd);
                    }

                    // Host includes brackets; callers use it only for allowed-IP checks and building the result
                    hostEnd = closeBracket + 1;
                    suffixEnd = closeBracket + 1;
                    if (closeBracket + 1 < input.Length && input[closeBracket + 1] == ':')
                    {
                        portStart = closeBracket + 2;
                        portEnd = input.Length;
                    }
                    else
                    {
                        portStart = 0;
                        portEnd = 0;
                    }

                    return true;
                }
            }

            // Try dot-separated IPv4 — handles "1.2.3.4", "1.2.3.4:port", and "1.2.3.4.suffix"
            if (ParseIPv4(input, '.', out var ipEnd))
            {
                SplitDottedHostPort(input, out hostEnd, out suffixEnd, out portStart, out portEnd);
                return true;
            }

            // Try underscore-separated IPv4 (e.g. "192_168_1_1" or "192_168_1_1.suffix")
            if (ParseIPv4(input, '_', out ipEnd))
            {
                hostEnd = ipEnd;
                suffixEnd = input.Length;
                portStart = 0;
                portEnd = 0;
                return true;
            }

            // Try hyphen-separated IPv4 (e.g. "192-168-1-1" or "192-168-1-1-suffix")
            if (ParseIPv4(input, '-', out ipEnd))
            {
                hostEnd = ipEnd;
                suffixEnd = input.Length;
                portStart = 0;
                portEnd = 0;
                return true;
            }

            // Try bare IPv6 (e.g. "2001:db8::1") — no port possible without brackets.
            // Use IPAddress.TryParse for IPv6 validation; the colon guard inside IsIPv6
            // ensures we skip the call entirely for hostnames.
            if (IsIPv6(input))
            {
                hostEnd = input.Length;
                suffixEnd = input.Length;
                portStart = 0;
                portEnd = 0;
                return true;
            }

            // Try host:port for non-IP hosts (e.g. "foo.dog:1234")
            var colonPos = input.IndexOf(':');
            if (colonPos > 0 && IsValidPort(input.Slice(colonPos + 1)))
            {
                // Check if the host part is an IP — this handles "10.0.0.1:8080" when it
                // didn't match the dot-IPv4 path above (shouldn't happen, but be safe)
                hostEnd = colonPos;
                suffixEnd = colonPos;
                portStart = colonPos + 1;
                portEnd = input.Length;
                return false;
            }

            return SetDefault(input.Length, out hostEnd, out suffixEnd, out portStart, out portEnd);
        }

        private static bool SetDefault(int inputLength, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
        {
            hostEnd = inputLength;
            suffixEnd = inputLength;
            portStart = 0;
            portEnd = 0;
            return false;
        }

        /// <summary>
        /// For dot-separated IPv4 input, splits into host (the 4-octet IP), suffix (trailing ".foo"), and port.
        /// </summary>
        private static void SplitDottedHostPort(ReadOnlySpan<char> input, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
        {
            // Check for colon (port separator)
            var colonIdx = input.IndexOf(':');
            ReadOnlySpan<char> withoutPort;
            if (colonIdx > 0)
            {
                withoutPort = input.Slice(0, colonIdx);
                portStart = colonIdx + 1;
                portEnd = input.Length;
            }
            else
            {
                withoutPort = input;
                portStart = 0;
                portEnd = 0;
            }

            // Find where the 4-octet IP ends within the dot-separated string
            var octetCount = 0;
            var i = 0;
            var lastIpEnd = 0;
            while (i < withoutPort.Length && octetCount < 4)
            {
                var octetStart = i;
                while (i < withoutPort.Length && CharHelpers.IsAsciiDigit(withoutPort[i]))
                {
                    i++;
                }

                if (i == octetStart)
                {
                    break;
                }

                var octetLen = i - octetStart;
                if (octetLen > 3 || !IsValidOctet(withoutPort.Slice(octetStart, octetLen)))
                {
                    break;
                }

                octetCount++;
                lastIpEnd = i;

                if (octetCount < 4 && i < withoutPort.Length && withoutPort[i] == '.')
                {
                    i++;
                }
                else
                {
                    break;
                }
            }

            if (octetCount == 4)
            {
                hostEnd = lastIpEnd;
                suffixEnd = withoutPort.Length;
            }
            else
            {
                hostEnd = withoutPort.Length;
                suffixEnd = withoutPort.Length;
            }
        }

        /// <summary>
        /// Checks if a host span is one of the allowed IP addresses that should not be blocked.
        /// </summary>
        private static bool IsAllowedIp(ReadOnlySpan<char> host)
        {
            return host.SequenceEqual("127.0.0.1".AsSpan()) ||
                   host.SequenceEqual("::1".AsSpan()) ||
                   host.SequenceEqual("169.254.169.254".AsSpan()) ||
                   host.SequenceEqual("fd00:ec2::254".AsSpan()) ||
                   host.SequenceEqual("169.254.170.2".AsSpan());
        }

        /// <summary>
        /// Checks if the span is a valid IPv6 address.
        /// Valid IPv6 always contains at least 2 colons (e.g. "::1"), so we use that as a
        /// cheap guard to skip the IPAddress.TryParse call for hostnames and other non-IPv6 strings.
        /// On older TFMs where IPAddress.TryParse requires a string, the colon guard ensures
        /// we only allocate for inputs that actually look like IPv6.
        /// </summary>
        private static bool IsIPv6(ReadOnlySpan<char> s)
        {
            var firstColon = s.IndexOf(':');
            if (firstColon < 0 || s.Slice(firstColon + 1).IndexOf(':') < 0)
            {
                return false;
            }

#if NETCOREAPP3_1_OR_GREATER
            return IPAddress.TryParse(s, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6;
#else
            return IPAddress.TryParse(s.ToString(), out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6;
#endif
        }

        /// <summary>
        /// Checks if the input starts with 4 valid octets separated by the given character.
        /// Returns true if found, with <paramref name="lastIndex"/> set to the index just past the 4th octet.
        /// The custom parser is needed because IPAddress.TryParse doesn't handle alternate
        /// separators ('_', '-'), and can't find IP boundaries within larger strings like "1.2.3.4.suffix".
        /// </summary>
        private static bool ParseIPv4(ReadOnlySpan<char> s, char sep, out int lastIndex)
        {
            lastIndex = 0;
            var octetCount = 0;
            var i = 0;

            while (i < s.Length && octetCount < 4)
            {
                var octetStart = i;
                while (i < s.Length && CharHelpers.IsAsciiDigit(s[i]))
                {
                    i++;
                }

                var octetLen = i - octetStart;
                if (octetLen == 0 || octetLen > 3 || !IsValidOctet(s.Slice(octetStart, octetLen)))
                {
                    return false;
                }

                octetCount++;

                if (octetCount < 4)
                {
                    if (i < s.Length && s[i] == sep)
                    {
                        i++;
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

        private static bool IsValidOctet(ReadOnlySpan<char> s)
        {
#if NETCOREAPP
            return byte.TryParse(s, out _);
#else
            var val = 0;
            for (var i = 0; i < s.Length; i++)
            {
                val = (val * 10) + (s[i] - '0');
            }

            return val <= 255;
#endif
        }

        private static bool IsValidPort(ReadOnlySpan<char> s)
        {
#if NETCOREAPP
            return ushort.TryParse(s, out _);
#else
            if (s.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < s.Length; i++)
            {
                if (!char.IsAsciiDigit(s[i]))
                {
                    return false;
                }
            }

            return true;
#endif
        }
    }
}
