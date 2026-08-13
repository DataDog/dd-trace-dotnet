// <copyright file="SemVer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Semantic version parsing and comparison matching the Rust/Eppo SemVer subset used by FFE.
/// This is a direct port of the Go implementation in dd-trace-go's openfeature/semver.go.
/// </summary>
internal static class SemVer
{
    /// <summary>
    /// Parses a semantic version string. Accepts the same syntax as Rust's semver::Version::parse.
    /// Core identifiers are limited to ulong, while numeric prerelease identifiers may be
    /// arbitrarily large. Build metadata is validated but not retained because it does not
    /// affect SemVer precedence.
    /// </summary>
    public static bool TryParse(string version, out ParsedSemVer result)
    {
        result = default;

        if (StringUtil.IsNullOrEmpty(version))
        {
            return false;
        }

        if (!TryParseCoreIdentifier(version, 0, out var major, out int next) ||
            next >= version.Length || version[next] != '.')
        {
            return false;
        }

        if (!TryParseCoreIdentifier(version, next + 1, out var minor, out next) ||
            next >= version.Length || version[next] != '.')
        {
            return false;
        }

        if (!TryParseCoreIdentifier(version, next + 1, out var patch, out next))
        {
            return false;
        }

        if (next == version.Length)
        {
            result = new ParsedSemVer(major, minor, patch, string.Empty);
            return true;
        }

        var remainder = version.Substring(next);
        var prerelease = string.Empty;

        if (remainder[0] == '-')
        {
            remainder = remainder.Substring(1);
            var buildStart = remainder.IndexOf('+');
            if (buildStart == -1)
            {
                if (!ValidSemverIdentifiers(remainder, allowLeadingZeros: false))
                {
                    return false;
                }

                result = new ParsedSemVer(major, minor, patch, remainder);
                return true;
            }

            prerelease = remainder.Substring(0, buildStart);
            if (!ValidSemverIdentifiers(prerelease, allowLeadingZeros: false))
            {
                return false;
            }

            remainder = remainder.Substring(buildStart + 1);
        }
        else if (remainder[0] == '+')
        {
            remainder = remainder.Substring(1);
        }
        else
        {
            return false;
        }

        if (!ValidSemverIdentifiers(remainder, allowLeadingZeros: true))
        {
            return false;
        }

        result = new ParsedSemVer(major, minor, patch, prerelease);
        return true;
    }

    /// <summary>
    /// Parses a core identifier (major, minor, or patch). Enforces ulong bounds without
    /// accepting shorthand or prefixes. Leading zeros are rejected (except for "0" itself).
    /// </summary>
    private static bool TryParseCoreIdentifier(string version, int start, out ulong value, out int next)
    {
        value = 0;
        next = start;

        if (start >= version.Length || !IsAsciiDigit(version[start]))
        {
            return false;
        }

        if (version[start] == '0')
        {
            next = start + 1;
            return true;
        }

        const ulong maxUint64 = ulong.MaxValue;
        var end = start;
        while (end < version.Length && IsAsciiDigit(version[end]))
        {
            var digit = (ulong)(version[end] - '0');
            if (value > (maxUint64 - digit) / 10)
            {
                // Overflow
                return false;
            }

            value = (value * 10) + digit;
            end++;
        }

        next = end;
        return true;
    }

    /// <summary>
    /// Validates dot-separated identifiers. Build metadata allows leading zeros;
    /// numeric prerelease identifiers reject them.
    /// </summary>
    private static bool ValidSemverIdentifiers(string value, bool allowLeadingZeros)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var identifierStart = 0;
        var identifierNumeric = true;

        for (var i = 0; i <= value.Length; i++)
        {
            if (i == value.Length || value[i] == '.')
            {
                if (i == identifierStart)
                {
                    // Empty identifier
                    return false;
                }

                if (!allowLeadingZeros && identifierNumeric && i - identifierStart > 1 && value[identifierStart] == '0')
                {
                    // Numeric identifier with leading zero
                    return false;
                }

                identifierStart = i + 1;
                identifierNumeric = true;
                continue;
            }

            if (!IsAsciiAlphanumeric(value[i]) && value[i] != '-')
            {
                return false;
            }

            if (!IsAsciiDigit(value[i]))
            {
                identifierNumeric = false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares SemVer precedence. Returns -1, 0, or 1.
    /// Build metadata is intentionally ignored.
    /// </summary>
    public static int Compare(ParsedSemVer left, ParsedSemVer right)
    {
        if (left.Major != right.Major)
        {
            return left.Major < right.Major ? -1 : 1;
        }

        if (left.Minor != right.Minor)
        {
            return left.Minor < right.Minor ? -1 : 1;
        }

        if (left.Patch != right.Patch)
        {
            return left.Patch < right.Patch ? -1 : 1;
        }

        return ComparePrerelease(left.Prerelease, right.Prerelease);
    }

    private static int ComparePrerelease(string left, string right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left.Length == 0)
        {
            return 1; // release > prerelease
        }

        if (right.Length == 0)
        {
            return -1; // prerelease < release
        }

        while (true)
        {
            NextIdentifier(left, out var leftIdentifier, out var leftRemainder);
            NextIdentifier(right, out var rightIdentifier, out var rightRemainder);

            var ordering = CompareIdentifier(leftIdentifier, rightIdentifier);
            if (ordering != 0)
            {
                return ordering;
            }

            if (leftRemainder.Length == 0)
            {
                if (rightRemainder.Length == 0)
                {
                    return 0;
                }

                return -1; // left has fewer identifiers
            }

            if (rightRemainder.Length == 0)
            {
                return 1; // right has fewer identifiers
            }

            // Skip the dot
            left = leftRemainder.Substring(1);
            right = rightRemainder.Substring(1);
        }
    }

    private static void NextIdentifier(string value, out string identifier, out string remainder)
    {
        var dot = value.IndexOf('.');
        if (dot != -1)
        {
            identifier = value.Substring(0, dot);
            remainder = value.Substring(dot);
        }
        else
        {
            identifier = value;
            remainder = string.Empty;
        }
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = IsNumericIdentifier(left);
        var rightNumeric = IsNumericIdentifier(right);

        if (leftNumeric && rightNumeric)
        {
            // Compare by length first (shorter = smaller), then lexicographically
            if (left.Length < right.Length)
            {
                return -1;
            }

            if (left.Length > right.Length)
            {
                return 1;
            }
        }
        else if (leftNumeric)
        {
            // Numeric identifiers always have lower precedence than alphanumeric
            return -1;
        }
        else if (rightNumeric)
        {
            return 1;
        }

        return string.Compare(left, right, StringComparison.Ordinal);
    }

    private static bool IsNumericIdentifier(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    private static bool IsAsciiAlphanumeric(char c) => IsAsciiDigit(c) || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}
