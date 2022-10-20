// <copyright file="HashBasedDeduplication.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

internal class HashBasedDeduplication
{
    public const int MaximumSize = 1000;
    private HashSet<int> vulnerabilityHashes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HashBasedDeduplication"/> class.
    /// For testing only.
    /// Note that this API does NOT replace the global HashBasedDeduplication instance.
    /// </summary>
    internal HashBasedDeduplication()
    {
    }

    public static HashBasedDeduplication Instance { get; } = new();

    public bool Add(Vulnerability vulnerability)
    {
        var hashCode = vulnerability.GetHashCode();

        bool newVulnerability;
        lock (vulnerabilityHashes)
        {
            newVulnerability = vulnerabilityHashes.Add(hashCode);
            if (newVulnerability && vulnerabilityHashes.Count > MaximumSize)
            {
                vulnerabilityHashes.Clear();
                vulnerabilityHashes.Add(hashCode);
            }
        }

        return newVulnerability;
    }
}
