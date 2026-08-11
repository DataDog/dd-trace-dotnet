// <copyright file="SemanticVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class SemanticVersion : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.Compiled);

    private readonly string _major;
    private readonly string _minor;
    private readonly string _patch;
    private readonly string[]? _prerelease;

    private SemanticVersion(Match match)
    {
        _major = match.Groups[1].Value;
        _minor = match.Groups[2].Value;
        _patch = match.Groups[3].Value;
        _prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : null;
    }

    public static bool TryParse(object? value, out SemanticVersion? version)
    {
        var match = value is string text ? Pattern.Match(text) : Match.Empty;
        version = match.Success && CoreIdentifiersFitUInt64(match) ? new SemanticVersion(match) : null;
        return version is not null;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = CompareNumeric(_major, other._major);
        if (result == 0) { result = CompareNumeric(_minor, other._minor); }
        if (result == 0) { result = CompareNumeric(_patch, other._patch); }
        if (result != 0 || (_prerelease is null && other._prerelease is null)) { return result; }
        if (_prerelease is null) { return 1; }
        if (other._prerelease is null) { return -1; }

        for (var index = 0; index < Math.Min(_prerelease.Length, other._prerelease.Length); index++)
        {
            result = CompareIdentifier(_prerelease[index], other._prerelease[index]);
            if (result != 0) { return result; }
        }

        return _prerelease.Length.CompareTo(other._prerelease.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = left.All(char.IsDigit);
        var rightNumeric = right.All(char.IsDigit);
        if (leftNumeric && rightNumeric) { return CompareNumeric(left, right); }
        if (leftNumeric != rightNumeric) { return leftNumeric ? -1 : 1; }
        return string.CompareOrdinal(left, right);
    }

    private static int CompareNumeric(string left, string right)
    {
        var result = left.Length.CompareTo(right.Length);
        return result == 0 ? string.CompareOrdinal(left, right) : result;
    }

    private static bool CoreIdentifiersFitUInt64(Match match)
    {
        return ulong.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && ulong.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            && ulong.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }
}
