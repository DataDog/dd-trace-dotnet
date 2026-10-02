// <copyright file="ServerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ServerConfiguration
{
    private Dictionary<string, bool>? _sourceConsent;

    [JsonConverter(typeof(StrictBooleanTrueJsonConverter))]
    public bool ObserveFullEvaluationData { get; set; }

    public string? CreatedAt { get; set; }

    public string? Format { get; set; }

    public Environment? Environment { get; set; }

    [JsonConverter(typeof(FlagCollectionJsonConverter))]
    public FlagCollection? Flags { get; set; }

    internal bool GetEvaluationConsent(string flagKey)
    {
        if (_sourceConsent is null)
        {
            return ObserveFullEvaluationData;
        }

        // A missing/invalid flag in a merged configuration has no consenting source.
        return flagKey is not null && _sourceConsent.TryGetValue(flagKey, out var consent) && consent;
    }

    internal void Merge(ServerConfiguration other)
    {
        if (_sourceConsent is null)
        {
            _sourceConsent = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (Flags is not null)
            {
                foreach (var pair in Flags.ValidFlags)
                {
                    _sourceConsent[pair.Key] = ObserveFullEvaluationData;
                }
            }
        }

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
            Flags = new FlagCollection();
        }

        if (other.Flags is not null)
        {
            // Keep consent with the source that supplied each flag without modifying shared flags.
            foreach (var pair in other.Flags.ValidFlags)
            {
                _sourceConsent[pair.Key] = other.GetEvaluationConsent(pair.Key);
            }

            foreach (var key in other.Flags.InvalidFlagKeys)
            {
                _sourceConsent.Remove(key);
            }

            Flags.Merge(other.Flags);
        }
    }
}
