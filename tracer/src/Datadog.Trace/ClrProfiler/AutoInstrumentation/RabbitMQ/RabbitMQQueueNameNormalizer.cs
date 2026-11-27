// <copyright file="RabbitMQQueueNameNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// Normalizes per-instance RabbitMQ queue and exchange names (server-named queues, direct reply-to,
/// per-process bus endpoints, names with a trailing UUID or long hex id, names that are only a UUID)
/// before they are used in Data Streams Monitoring edge tags. Without this, every process or request
/// creates new DSM edges.
/// Only used for DSM edge tags; span tags and resource names keep the raw name.
/// </summary>
internal static class RabbitMQQueueNameNormalizer
{
    private const string AmqGenPrefix = "amq.gen-";
    private const string AmqGenNormalized = "amq.gen-*";
    private const string ReplyToPrefix = "amq.rabbitmq.reply-to";
    private const string BusMarker = "_bus_";
    private const string WolverinePrefix = "wolverine.";
    private const string WolverineControl = "wolverine.control.*";
    private const string WolverineResponse = "wolverine.response.*";
    private const string WolverineReplies = "wolverine.replies.*";
    private const int MinHexSuffixLength = 16;
    private const int DashedUuidLength = 36;
    private const int UndashedUuidLength = 32;
    private const string BareUuidNormalized = "*";

    /// <summary>
    /// Returns the normalized name, or the same instance when no rule applies.
    /// Allocates only when a per-instance suffix has to be replaced.
    /// </summary>
    public static string? Normalize(string? name)
    {
        if (name is null || name.Length == 0)
        {
            return name;
        }

        if (name.StartsWith(AmqGenPrefix, StringComparison.Ordinal))
        {
            return AmqGenNormalized;
        }

        if (name.StartsWith(ReplyToPrefix, StringComparison.Ordinal))
        {
            return ReplyToPrefix;
        }

        if (TryNormalizeBusEndpoint(name, out var normalized)
         || TryNormalizeWolverine(name, out normalized)
         || TryNormalizeIdSuffix(name, out normalized))
        {
            return normalized;
        }

        if (IsBareUuid(name))
        {
            return BareUuidNormalized;
        }

        return name;
    }

    // ^(.+)_(.+)_bus_([a-z0-9]+)$ (case-insensitive) => *_<group 2>_bus_*
    private static bool TryNormalizeBusEndpoint(string name, out string? normalized)
    {
        normalized = null;
        var busIndex = name.LastIndexOf(BusMarker, StringComparison.OrdinalIgnoreCase);
        var suffixStart = busIndex + BusMarker.Length;

        // need at least "a_b" before the marker and a non-empty suffix after it
        if (busIndex < 3 || suffixStart >= name.Length)
        {
            return false;
        }

        for (var i = suffixStart; i < name.Length; i++)
        {
            if (!IsAsciiLetterOrDigit(name[i]))
            {
                return false;
            }
        }

        // group 1 is greedy, so group 2 starts after the last '_' that leaves both groups non-empty
        var separator = -1;
        for (var i = busIndex - 2; i >= 1; i--)
        {
            if (name[i] == '_')
            {
                separator = i;
                break;
            }
        }

        if (separator < 0)
        {
            return false;
        }

        // "*_" + group 2 + "_bus_*"
        normalized = string.Concat("*", name.Substring(separator, suffixStart - separator), "*");
        return true;
    }

    // ^wolverine\.(control|response|replies)\.(.+)$ => wolverine.<group 1>.*
    private static bool TryNormalizeWolverine(string name, out string? normalized)
    {
        normalized = null;
        if (!name.StartsWith(WolverinePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // each normalized form is "wolverine.<kind>.*", so its length minus 1 is the prefix length
        normalized = MatchWolverine(name, WolverineControl)
                  ?? MatchWolverine(name, WolverineResponse)
                  ?? MatchWolverine(name, WolverineReplies);
        return normalized is not null;

        static string? MatchWolverine(string name, string normalizedForm)
        {
            var prefixLength = normalizedForm.Length - 1;
            return name.Length > prefixLength && string.CompareOrdinal(name, 0, normalizedForm, 0, prefixLength) == 0
                       ? normalizedForm
                       : null;
        }
    }

    // trailing [-_.] followed by a UUID (dashed or 32 hex) or by 16+ hex chars => <prefix><sep>*
    private static bool TryNormalizeIdSuffix(string name, out string? normalized)
    {
        normalized = null;
        var separator = -1;

        // undashed UUIDs are 32 hex chars, so they are covered by the 16+ hex rule
        var hexStart = name.Length;
        while (hexStart > 0 && IsHex(name[hexStart - 1]))
        {
            hexStart--;
        }

        if (name.Length - hexStart >= MinHexSuffixLength && hexStart > 0 && IsSeparator(name[hexStart - 1]))
        {
            separator = hexStart - 1;
        }
        else if (name.Length > DashedUuidLength
              && IsDashedUuid(name, name.Length - DashedUuidLength)
              && IsSeparator(name[name.Length - DashedUuidLength - 1]))
        {
            separator = name.Length - DashedUuidLength - 1;
        }

        if (separator < 0)
        {
            return false;
        }

        normalized = string.Concat(name.Substring(0, separator + 1), "*");
        return true;
    }

    // the whole name is a UUID: 8-4-4-4-12 hex with dashes, or exactly 32 hex chars => *
    private static bool IsBareUuid(string name)
    {
        if (name.Length == DashedUuidLength)
        {
            return IsDashedUuid(name, 0);
        }

        if (name.Length != UndashedUuidLength)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!IsHex(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDashedUuid(string name, int start)
    {
        // 8-4-4-4-12
        for (var i = 0; i < DashedUuidLength; i++)
        {
            var c = name[start + i];
            if (i is 8 or 13 or 18 or 23)
            {
                if (c != '-')
                {
                    return false;
                }
            }
            else if (!IsHex(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSeparator(char c) => c is '-' or '_' or '.';

    private static bool IsHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static bool IsAsciiLetterOrDigit(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
