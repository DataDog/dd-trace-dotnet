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
    public string? CreatedAt { get; set; }

    public string? Format { get; set; }

    public Environment? Environment { get; set; }

    public Dictionary<string, Flag>? Flags { get; set; }

    internal void Merge(ServerConfiguration other)
    {
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
