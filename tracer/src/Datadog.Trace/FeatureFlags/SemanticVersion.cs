// <copyright file="SemanticVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags;

internal readonly struct SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _core;
    private readonly string[] _prerelease;

    private SemanticVersion(string[] core, string[] prerelease)
    {
        _core = core;
        _prerelease = prerelease;
    }

    public int CompareTo(SemanticVersion other)
    {
        var coreLength = Math.Max(_core.Length, other._core.Length);
        for (var i = 0; i < coreLength; i++)
        {
            var left = i < _core.Length ? _core[i] : "0";
            var right = i < other._core.Length ? other._core[i] : "0";
            var coreComparison = CompareNumericIdentifier(left, right);
            if (coreComparison != 0)
            {
                return coreComparison;
            }
        }

        var result = 0;

        if (_prerelease.Length == 0 || other._prerelease.Length == 0)
        {
            return other._prerelease.Length.CompareTo(_prerelease.Length);
        }

        var count = Math.Min(_prerelease.Length, other._prerelease.Length);
        for (var i = 0; i < count; i++)
        {
            var left = _prerelease[i];
            var right = other._prerelease[i];
            var leftIsNumeric = IsNumeric(left);
            var rightIsNumeric = IsNumeric(right);
            result = leftIsNumeric && rightIsNumeric
                         ? CompareNumericIdentifier(left, right)
                         : leftIsNumeric
                             ? -1
                             : rightIsNumeric
                                 ? 1
                                 : string.CompareOrdinal(left, right);
            if (result != 0)
            {
                return result;
            }
        }

        return _prerelease.Length.CompareTo(other._prerelease.Length);
    }

    internal static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (value is null || value.Length == 0)
        {
            return false;
        }

        var buildSeparator = value.IndexOf('+');
        if (buildSeparator >= 0)
        {
            if (value.IndexOf('+', buildSeparator + 1) >= 0 || !AreValidIdentifiers(value.Substring(buildSeparator + 1), allowNumericLeadingZeros: true))
            {
                return false;
            }

            value = value.Substring(0, buildSeparator);
        }

        string[] prerelease = [];
        var prereleaseSeparator = value.IndexOf('-');
        if (prereleaseSeparator >= 0)
        {
            var prereleaseText = value.Substring(prereleaseSeparator + 1);
            if (!AreValidIdentifiers(prereleaseText, allowNumericLeadingZeros: false))
            {
                return false;
            }

            prerelease = prereleaseText.Split('.');
            value = value.Substring(0, prereleaseSeparator);
        }

        var core = value.Split('.');
        if (core.Length == 0 || Array.Exists(core, static identifier => !IsValidCoreIdentifier(identifier)))
        {
            return false;
        }

        version = new SemanticVersion(core, prerelease);
        return true;
    }

    private static bool AreValidIdentifiers(string value, bool allowNumericLeadingZeros)
    {
        var identifiers = value.Split('.');
        foreach (var identifier in identifiers)
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            foreach (var character in identifier)
            {
                if (!(character is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '-'))
                {
                    return false;
                }
            }

            if (!allowNumericLeadingZeros && IsNumeric(identifier) && identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCoreIdentifier(string value) =>
        IsNumeric(value) &&
        (value.Length == 1 || value[0] != '0') &&
        ulong.TryParse(value, out _);

    private static bool IsNumeric(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareNumericIdentifier(string left, string right) =>
        left.Length != right.Length ? left.Length.CompareTo(right.Length) : string.CompareOrdinal(left, right);
}
