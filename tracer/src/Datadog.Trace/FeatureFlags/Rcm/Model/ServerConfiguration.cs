// <copyright file="ServerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ServerConfiguration
{
    private bool _validated;

    public string? CreatedAt { get; set; }

    public string? Format { get; set; }

    public Environment? Environment { get; set; }

    public Dictionary<string, Flag>? Flags { get; set; }

    /// <summary>
    /// Gets or sets the collection of flags that failed validation (e.g., invalid SemVer comparands).
    /// Maps flag key to an error message describing the validation failure.
    /// Populated by <see cref="Validate"/>.
    /// </summary>
    internal Dictionary<string, string>? InvalidFlags { get; set; }

    /// <summary>
    /// Validates all flags by pre-parsing SemVer comparands and identifying
    /// flags with invalid configuration. This is called eagerly during config
    /// loading (before evaluation) so that invalid flags can be detected and
    /// reported without waiting for an evaluation to trigger the error.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    internal void Validate()
    {
        if (_validated)
        {
            return;
        }

        _validated = true;

        if (Flags is null)
        {
            return;
        }

        foreach (var pair in Flags)
        {
            var flagKey = pair.Key;
            var flag = pair.Value;
            if (flag?.Allocations is null)
            {
                continue;
            }

            foreach (var allocation in flag.Allocations)
            {
                if (allocation.Rules is null)
                {
                    continue;
                }

                foreach (var rule in allocation.Rules)
                {
                    if (rule.Conditions is null)
                    {
                        continue;
                    }

                    foreach (var condition in rule.Conditions)
                    {
                        if (!condition.TryPreparseSemverComparand())
                        {
                            InvalidFlags ??= new Dictionary<string, string>();
                            InvalidFlags[flagKey] = $"Invalid semantic version comparand for flag \"{flagKey}\"";
                            break;
                        }
                    }

                    if (InvalidFlags is not null && InvalidFlags.ContainsKey(flagKey))
                    {
                        break;
                    }
                }

                if (InvalidFlags is not null && InvalidFlags.ContainsKey(flagKey))
                {
                    break;
                }
            }
        }
    }

    internal void Merge(ServerConfiguration other)
    {
        // Merging changes the flag set, so the merged config needs re-validation.
        _validated = false;
        InvalidFlags = null;

        if (other.CreatedAt is not null)
        {
            CreatedAt = other.CreatedAt;
        }

        if (other.Format is not null)
        {
            Format = other.Format;
        }

        if (other.Environment is not null)
        {
            Environment = other.Environment;
        }

        if (Flags is null)
        {
            Flags = new Dictionary<string, Flag>();
        }

        if (other.Flags is not null)
        {
            foreach (var pair in other.Flags)
            {
                Flags[pair.Key] = pair.Value;
            }
        }
    }
}
