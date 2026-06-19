// <copyright file="IpAddressObfuscationUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

            // Dedup uses two separate seen lists indexed into the original 'raw' string,
            // avoiding StringBuilder indexer access (which is O(chunks) per character).
            // Cross-list duplicates can't occur: blocked results always contain "blocked-ip-address", non-blocked never do.
            List<KeyValuePair<int, int>>? seenRaw = null;
            List<BlockedIpParts>? seenBlocked = null;
            var sb = StringBuilderCache.Acquire();
            var remaining = raw.AsSpan();
            var position = 0;

            while (remaining.Length > 0)
            {
                // Get the next entry as a span of the original string
                ReadOnlySpan<char> entry;
                var commaIndex = remaining.IndexOf(',');
                if (commaIndex < 0)
                {
                    entry = remaining;
                    remaining = default;
                }
                else
                {
                    entry = remaining.Slice(0, commaIndex);
                    remaining = remaining.Slice(commaIndex + 1);
                }

                var rawOffset = position;
                position += entry.Length + 1; // +1 for comma separator

                if (IsBlockedIp(entry, out var prefixLength, out var isHostBracketed, out var hostEnd, out var suffixEnd, out var portStart, out var portEnd))
                {
                    // Blocked IP: check duplicate by comparing the variable parts (prefix, suffix, port)
                    // as spans of the original string — the "blocked-ip-address" middle is constant.
                    var afterOffset = rawOffset + prefixLength;
                    var regionEnd = isHostBracketed ? hostEnd + 1 : hostEnd;
                    var parts = new BlockedIpParts(rawOffset, prefixLength, afterOffset + regionEnd, suffixEnd - regionEnd, afterOffset + portStart, portEnd - portStart);
                    if (seenBlocked is null || !IsDuplicateBlocked(raw, in parts, seenBlocked))
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(',');
                        }

                        AppendQuantizedEntry(sb, entry, prefixLength, isHostBracketed, hostEnd, suffixEnd, portStart, portEnd, blocked: true);
                        seenBlocked ??= new();
                        seenBlocked.Add(parts);
                    }
                }
                else if (seenRaw is null || !IsDuplicateRaw(raw, rawOffset, entry.Length, seenRaw))
                {
                    // Non-blocked: quantized result == original entry, compare spans directly
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }

                    seenRaw ??= new();
                    seenRaw.Add(new KeyValuePair<int, int>(rawOffset, entry.Length));
                    AppendQuantizedEntry(sb, entry, prefixLength, isHostBracketed, hostEnd, suffixEnd, portStart, portEnd, blocked: false);
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
            var blocked = IsBlockedIp(span, out var prefixLength, out var isHostBracketed, out var hostEnd, out var suffixEnd, out var portStart, out var portEnd);

            if (!blocked && !isHostBracketed)
            {
                // No change needed — return original string (zero-alloc fast path)
                return raw;
            }

            var sb = StringBuilderCache.Acquire();
            AppendQuantizedEntry(sb, span, prefixLength, isHostBracketed, hostEnd, suffixEnd, portStart, portEnd, blocked);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Determines whether an entry contains a non-allowed IP that should be blocked.
        /// When true, the output indices describe the prefix, host, suffix, and port regions
        /// relative to the after-prefix portion of <paramref name="raw"/> for building the blocked result.
        /// <paramref name="isHostBracketed"/> indicates the host was wrapped in brackets (e.g. "[::1]:8080");
        /// callers use this to skip '[' and ']' when building output.
        /// </summary>
        private static bool IsBlockedIp(
            ReadOnlySpan<char> raw,
            out int prefixLength,
            out bool isHostBracketed,
            out int hostEnd,
            out int suffixEnd,
            out int portStart,
            out int portEnd)
        {
            prefixLength = GetPrefixLength(raw);
            var after = raw.Slice(prefixLength);

            if (!ParseIpAndPort(after, out var hostStart, out hostEnd, out suffixEnd, out portStart, out portEnd))
            {
                isHostBracketed = false;
                return false;
            }

            isHostBracketed = hostStart > 0;
            return !IsAllowedIp(after.Slice(hostStart, hostEnd - hostStart));
        }

        /// <summary>
        /// Appends the quantized entry to <paramref name="sb"/>.
        /// For blocked IPs, replaces the host with "blocked-ip-address".
        /// For allowed entries, outputs the original host content (without brackets).
        /// When <paramref name="isHostBracketed"/> is true, '[' before the host and ']' after it are skipped.
        /// </summary>
        private static void AppendQuantizedEntry(
            StringBuilder sb,
            ReadOnlySpan<char> raw,
            int prefixLength,
            bool isHostBracketed,
            int hostEnd,
            int suffixEnd,
            int portStart,
            int portEnd,
            bool blocked)
        {
            var after = raw.Slice(prefixLength);
            var hostStart = isHostBracketed ? 1 : 0;
            var regionEnd = isHostBracketed ? hostEnd + 1 : hostEnd;

            sb.Append(raw.Slice(0, prefixLength));

            if (blocked)
            {
                sb.Append(BlockedIpAddress);
            }
            else
            {
                sb.Append(after.Slice(hostStart, hostEnd - hostStart));
            }

            sb.Append(after.Slice(regionEnd, suffixEnd - regionEnd));
            if (portEnd > portStart)
            {
                sb.Append(':');
                sb.Append(after.Slice(portStart, portEnd - portStart));
            }
        }

        private static bool IsDuplicateRaw(string raw, int candidateOffset, int candidateLength, List<KeyValuePair<int, int>> seenRaw)
        {
            var candidate = raw.AsSpan(candidateOffset, candidateLength);
            foreach (var existing in seenRaw)
            {
                if (raw.AsSpan(existing.Key, existing.Value).SequenceEqual(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDuplicateBlocked(string raw, in BlockedIpParts candidate, List<BlockedIpParts> seenBlocked)
        {
            foreach (var existing in seenBlocked)
            {
                if (raw.AsSpan(existing.PrefixOffset, existing.PrefixLength).SequenceEqual(raw.AsSpan(candidate.PrefixOffset, candidate.PrefixLength))
                  && raw.AsSpan(existing.SuffixOffset, existing.SuffixLength).SequenceEqual(raw.AsSpan(candidate.SuffixOffset, candidate.SuffixLength))
                  && raw.AsSpan(existing.PortOffset, existing.PortLength).SequenceEqual(raw.AsSpan(candidate.PortOffset, candidate.PortLength)))
                {
                    return true;
                }
            }

            return false;
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
        /// host=[hostStart..hostEnd), suffix=[regionEnd..suffixEnd), port=[portStart..portEnd).
        /// For bracketed IPv6, hostStart skips '[' and hostEnd stops before ']';
        /// regionEnd (= hostStart > 0 ? hostEnd + 1 : hostEnd) skips past ']' for suffix positioning.
        /// </summary>
        private static bool ParseIpAndPort(ReadOnlySpan<char> input, out int hostStart, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
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
                        return SetDefault(input.Length, out hostStart, out hostEnd, out suffixEnd, out portStart, out portEnd);
                    }

                    // hostStart/hostEnd exclude brackets; callers derive regionEnd to skip past ']'
                    hostStart = 1;
                    hostEnd = closeBracket;
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

            hostStart = 0;

            // Try dot-separated IPv4 — handles "1.2.3.4", "1.2.3.4:port", and "1.2.3.4.suffix"
            if (ParseIPv4(input, '.', out var ipEnd))
            {
                SplitDottedHostPort(input, ipEnd, out hostEnd, out suffixEnd, out portStart, out portEnd);
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

            // Try host:port for non-IP hosts (e.g. "foo.dog:1234").
            // All IP formats (IPv4 dotted/underscore/hyphen, bracketed IPv6, bare IPv6)
            // have already been tried above, so this is a hostname with a port.
            var colonIndex = input.IndexOf(':');
            if (colonIndex > 0 && IsValidPort(input.Slice(colonIndex + 1)))
            {
                hostEnd = colonIndex;
                suffixEnd = colonIndex;
                portStart = colonIndex + 1;
                portEnd = input.Length;
                return false;
            }

            return SetDefault(input.Length, out hostStart, out hostEnd, out suffixEnd, out portStart, out portEnd);
        }

        private static bool SetDefault(int inputLength, out int hostStart, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
        {
            hostStart = 0;
            hostEnd = inputLength;
            suffixEnd = inputLength;
            portStart = 0;
            portEnd = 0;
            return false;
        }

        /// <summary>
        /// For dot-separated IPv4 input, uses the already-known IP end position
        /// to split into host, suffix, and port regions.
        /// </summary>
        private static void SplitDottedHostPort(ReadOnlySpan<char> input, int ipEnd, out int hostEnd, out int suffixEnd, out int portStart, out int portEnd)
        {
            hostEnd = ipEnd;

            // Check whether the content after the IP is a port (":1234") or suffix (".foo")
            var colonIndex = input.IndexOf(':');
            if (colonIndex >= ipEnd && IsValidPort(input.Slice(colonIndex + 1)))
            {
                // Everything between IP end and colon is suffix, everything after colon is port
                suffixEnd = colonIndex;
                portStart = colonIndex + 1;
                portEnd = input.Length;
            }
            else
            {
                // No valid port — treat everything after IP end as suffix (including invalid ports like ":999999")
                suffixEnd = input.Length;
                portStart = 0;
                portEnd = 0;
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
            // Yeah, this is nasty, but the parsing rules for ipv6 are too gnarly
            return IPAddress.TryParse(s.ToString(), out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6;
#endif
        }

        /// <summary>
        /// Checks if the input starts with 4 valid octets separated by the given character.
        /// Returns true if found, with <paramref name="lastIndex"/> set to the index just past the 4th octet.
        /// The custom parser is needed because IPAddress.TryParse doesn't handle alternate
        /// separators ('_', '-'), and can't find IP boundaries within larger strings like "1.2.3.4.suffix".
        /// </summary>
        private static bool ParseIPv4(ReadOnlySpan<char> s, char separator, out int lastIndex)
        {
            lastIndex = 0;
            var remaining = s;
            var pos = 0;

            for (var octet = 0; octet < 4; octet++)
            {
                ReadOnlySpan<char> octetSpan;
                var separatorIndex = remaining.IndexOf(separator);

                if (octet < 3)
                {
                    // First 3 octets must be followed by a separator
                    if (separatorIndex <= 0)
                    {
                        return false;
                    }

                    octetSpan = remaining.Slice(0, separatorIndex);
                    if (!IsValidOctet(octetSpan))
                    {
                        return false;
                    }

                    pos += separatorIndex + 1;
                    remaining = remaining.Slice(separatorIndex + 1);
                }
                else
                {
                    // 4th octet: scan leading digits only — anything after (port, suffix) is not part of the octet.
                    var octetEnd = 0;
                    while (octetEnd < remaining.Length && char.IsAsciiDigit(remaining[octetEnd]))
                    {
                        octetEnd++;
                    }

                    octetSpan = remaining.Slice(0, octetEnd);
                    if (!IsValidOctet(octetSpan))
                    {
                        return false;
                    }

                    pos += octetEnd;
                }
            }

            lastIndex = pos;
            return true;
        }

        private static bool IsValidOctet(ReadOnlySpan<char> s)
        {
            if (s.Length == 0 || s.Length > 3)
            {
                return false;
            }

#if NETCOREAPP
            return byte.TryParse(s, out _);
#else
            var val = 0;
            for (var i = 0; i < s.Length; i++)
            {
                if (!char.IsAsciiDigit(s[i]))
                {
                    return false;
                }

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
            // This doesn't strictly check the number is a valid ushort, but it's "good enough"
            if (s.Length == 0 || s.Length > 5)
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

        /// <summary>
        /// Stores the variable parts of a blocked IP result as offsets into the original string.
        /// The blocked result is: prefix + "blocked-ip-address" + suffix + ":" + port.
        /// Two blocked results are equal iff their prefix, suffix, and port spans are equal.
        /// </summary>
        private readonly struct BlockedIpParts(int prefixOffset, int prefixLength, int suffixOffset, int suffixLength, int portOffset, int portLength)
        {
            public readonly int PrefixOffset = prefixOffset;
            public readonly int PrefixLength = prefixLength;
            public readonly int SuffixOffset = suffixOffset;
            public readonly int SuffixLength = suffixLength;
            public readonly int PortOffset = portOffset;
            public readonly int PortLength = portLength;
        }
    }
}
