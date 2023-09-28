// <copyright file="Data.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Rcm.Models.AsmData;

internal class Data
{
    /// <summary>
    /// Gets or sets an integer representing a UNIX timestamp. Past this timestamp, the entry is ignored. Without a timestamp a value never expires.
    /// </summary>
    public ulong? Expiration { get; set; }

    public string? Value { get; set; }

    public List<KeyValuePair<string, object?>> ToKeyValuePair()
    {
        List<KeyValuePair<string, object?>> data = new() { new("value", Value) };
        if (Expiration.HasValue)
        {
            data.Add(new("expiration", Expiration));
        }

        return data;
    }
}
