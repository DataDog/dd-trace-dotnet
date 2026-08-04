// <copyright file="SemanticVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// A semantic version parser and comparer that matches the UFC evaluator's ordering.
/// Semantic-version precedence is followed by build-metadata ordering to provide a total ordering.
/// </summary>
internal readonly struct SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string? _value;
    private readonly ulong _major;
    private readonly ulong _minor;
    private readonly ulong _patch;
    private readonly int _prereleaseStart;
    private readonly int _prereleaseLength;
    private readonly int _buildStart;
    private readonly int _buildLength;

    private SemanticVersion(
        string value,
        ulong major,
        ulong minor,
        ulong patch,
        int prereleaseStart,
        int prereleaseLength,
        int buildStart,
        int buildLength)
    {
        _value = value;
        _major = major;
        _minor = minor;
        _patch = patch;
        _prereleaseStart = prereleaseStart;
        _prereleaseLength = prereleaseLength;
        _buildStart = buildStart;
        _buildLength = buildLength;
    }

    public int CompareTo(SemanticVersion other)
    {
        var ordering = _major.CompareTo(other._major);
        if (ordering != 0)
        {
            return ordering;
        }

        ordering = _minor.CompareTo(other._minor);
        if (ordering != 0)
        {
            return ordering;
        }

        ordering = _patch.CompareTo(other._patch);
        if (ordering != 0)
        {
            return ordering;
        }

        ordering = ComparePrerelease(this, other);
        if (ordering != 0)
        {
            return ordering;
        }

        return CompareIdentifiers(
            _value!,
            _buildStart,
            _buildLength,
            other._value!,
            other._buildStart,
            other._buildLength,
            compareLeadingZeroCount: true);
    }

    internal static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (StringUtil.IsNullOrEmpty(value))
        {
            return false;
        }

        var input = value!;
        var index = 0;
        if (!TryParseCoreNumber(input, ref index, out var major)
         || !TryConsume(input, ref index, '.')
         || !TryParseCoreNumber(input, ref index, out var minor)
         || !TryConsume(input, ref index, '.')
         || !TryParseCoreNumber(input, ref index, out var patch))
        {
            return false;
        }

        var prereleaseStart = 0;
        var prereleaseLength = 0;
        if (TryConsume(input, ref index, '-'))
        {
            prereleaseStart = index;
            if (!TryParseIdentifiers(input, ref index, '+', prohibitNumericLeadingZeros: true))
            {
                return false;
            }

            prereleaseLength = index - prereleaseStart;
        }

        var buildStart = 0;
        var buildLength = 0;
        if (TryConsume(input, ref index, '+'))
        {
            buildStart = index;
            if (!TryParseIdentifiers(input, ref index, terminator: null, prohibitNumericLeadingZeros: false))
            {
                return false;
            }

            buildLength = index - buildStart;
        }

        if (index != input.Length)
        {
            return false;
        }

        version = new SemanticVersion(input, major, minor, patch, prereleaseStart, prereleaseLength, buildStart, buildLength);
        return true;
    }

    private static bool TryParseCoreNumber(string value, ref int index, out ulong result)
    {
        result = 0;
        var start = index;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            if (index > start && value[start] == '0')
            {
                return false;
            }

            var digit = (uint)(value[index] - '0');
            if (result > (ulong.MaxValue - digit) / 10)
            {
                return false;
            }

            result = (result * 10) + digit;
            index++;
        }

        return index > start;
    }

    private static bool TryParseIdentifiers(string value, ref int index, char? terminator, bool prohibitNumericLeadingZeros)
    {
        while (index < value.Length)
        {
            var identifierStart = index;
            var numeric = true;
            while (index < value.Length
                && value[index] != '.'
                && (!terminator.HasValue || value[index] != terminator.Value))
            {
                var character = value[index];
                if (!IsIdentifierCharacter(character))
                {
                    return false;
                }

                numeric &= IsAsciiDigit(character);
                index++;
            }

            var identifierLength = index - identifierStart;
            if (identifierLength == 0
             || (prohibitNumericLeadingZeros && numeric && identifierLength > 1 && value[identifierStart] == '0'))
            {
                return false;
            }

            if (index == value.Length || (terminator.HasValue && value[index] == terminator.Value))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool TryConsume(string value, ref int index, char expected)
    {
        if (index >= value.Length || value[index] != expected)
        {
            return false;
        }

        index++;
        return true;
    }

    private static int ComparePrerelease(SemanticVersion left, SemanticVersion right)
    {
        if (left._prereleaseLength == 0)
        {
            return right._prereleaseLength == 0 ? 0 : 1;
        }

        if (right._prereleaseLength == 0)
        {
            return -1;
        }

        return CompareIdentifiers(
            left._value!,
            left._prereleaseStart,
            left._prereleaseLength,
            right._value!,
            right._prereleaseStart,
            right._prereleaseLength,
            compareLeadingZeroCount: false);
    }

    private static int CompareIdentifiers(
        string left,
        int leftStart,
        int leftLength,
        string right,
        int rightStart,
        int rightLength,
        bool compareLeadingZeroCount)
    {
        if (leftLength == 0)
        {
            return rightLength == 0 ? 0 : -1;
        }

        if (rightLength == 0)
        {
            return 1;
        }

        var leftEnd = leftStart + leftLength;
        var rightEnd = rightStart + rightLength;
        while (true)
        {
            var leftIdentifierEnd = FindIdentifierEnd(left, leftStart, leftEnd);
            var rightIdentifierEnd = FindIdentifierEnd(right, rightStart, rightEnd);
            var ordering = CompareIdentifier(
                left,
                leftStart,
                leftIdentifierEnd - leftStart,
                right,
                rightStart,
                rightIdentifierEnd - rightStart,
                compareLeadingZeroCount);
            if (ordering != 0)
            {
                return ordering;
            }

            var leftComplete = leftIdentifierEnd == leftEnd;
            var rightComplete = rightIdentifierEnd == rightEnd;
            if (leftComplete || rightComplete)
            {
                return leftComplete == rightComplete ? 0 : leftComplete ? -1 : 1;
            }

            leftStart = leftIdentifierEnd + 1;
            rightStart = rightIdentifierEnd + 1;
        }
    }

    private static int CompareIdentifier(
        string left,
        int leftStart,
        int leftLength,
        string right,
        int rightStart,
        int rightLength,
        bool compareLeadingZeroCount)
    {
        var leftNumeric = IsAsciiDigits(left, leftStart, leftLength);
        var rightNumeric = IsAsciiDigits(right, rightStart, rightLength);
        if (leftNumeric && rightNumeric)
        {
            var leftNumericStart = SkipLeadingZeros(left, leftStart, leftLength);
            var rightNumericStart = SkipLeadingZeros(right, rightStart, rightLength);
            var leftNumericLength = (leftStart + leftLength) - leftNumericStart;
            var rightNumericLength = (rightStart + rightLength) - rightNumericStart;

            var ordering = leftNumericLength.CompareTo(rightNumericLength);
            if (ordering != 0)
            {
                return ordering;
            }

            ordering = CompareOrdinal(left, leftNumericStart, leftNumericLength, right, rightNumericStart, rightNumericLength);
            if (ordering != 0 || !compareLeadingZeroCount)
            {
                return ordering;
            }

            return leftLength.CompareTo(rightLength);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return CompareOrdinal(left, leftStart, leftLength, right, rightStart, rightLength);
    }

    private static int CompareOrdinal(string left, int leftStart, int leftLength, string right, int rightStart, int rightLength)
    {
        var commonLength = Math.Min(leftLength, rightLength);
        for (var i = 0; i < commonLength; i++)
        {
            var ordering = left[leftStart + i].CompareTo(right[rightStart + i]);
            if (ordering != 0)
            {
                return ordering;
            }
        }

        return leftLength.CompareTo(rightLength);
    }

    private static int FindIdentifierEnd(string value, int start, int end)
    {
        while (start < end && value[start] != '.')
        {
            start++;
        }

        return start;
    }

    private static int SkipLeadingZeros(string value, int start, int length)
    {
        var end = start + length;
        while (start < end && value[start] == '0')
        {
            start++;
        }

        return start;
    }

    private static bool IsAsciiDigits(string value, int start, int length)
    {
        var end = start + length;
        while (start < end)
        {
            if (!IsAsciiDigit(value[start]))
            {
                return false;
            }

            start++;
        }

        return true;
    }

    private static bool IsIdentifierCharacter(char value) => IsAsciiDigit(value)
                                                          || (value >= 'A' && value <= 'Z')
                                                          || (value >= 'a' && value <= 'z')
                                                          || value == '-';

    private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';
}
