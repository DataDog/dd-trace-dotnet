// <copyright file="HashBasedDeduplication.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.Iast;

internal class HashBasedDeduplication
{
    public const int MaximumSize = 1000;
    public const int MinutesToClearCache = 60;
    private HashSet<int> _vulnerabilityHashes = new();
    private DateTime _cacheClearedTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashBasedDeduplication"/> class.
    /// For testing only.
    /// Note that this API does NOT replace the global HashBasedDeduplication instance.
    /// </summary>
    internal HashBasedDeduplication(DateTime? currentTime = null)
    {
        _cacheClearedTime = currentTime ?? DateTime.UtcNow;
    }

    public static HashBasedDeduplication Instance { get; } = new();

    public bool Add(Vulnerability vulnerability, DateTime? addTime = null)
    {
        var hashCode = vulnerability.GetHashCode();
        var currentTime = addTime ?? DateTime.UtcNow;

        bool newVulnerability;
        lock (_vulnerabilityHashes)
        {
            if ((currentTime - _cacheClearedTime).TotalMinutes >= MinutesToClearCache)
            {
                _vulnerabilityHashes.Clear();
                _cacheClearedTime = currentTime;
            }

            newVulnerability = _vulnerabilityHashes.Add(hashCode);

            if (newVulnerability && _vulnerabilityHashes.Count > MaximumSize)
            {
                _vulnerabilityHashes.Clear();
                _cacheClearedTime = currentTime;
                _vulnerabilityHashes.Add(hashCode);
            }
        }

        return newVulnerability;
    }
}
