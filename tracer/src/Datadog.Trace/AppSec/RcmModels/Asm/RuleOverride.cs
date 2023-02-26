// <copyright file="RuleOverride.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.RcmModels.Asm;

internal class RuleOverride
{
    public string? Id { get; set; }

    public bool? Enabled { get; set; }

    [JsonProperty("on_match")]
    public string[]? OnMatch { get; set; }

    public override string ToString()
    {
        return $"{{{Id} : {Enabled}, on match actions: {string.Join(",", OnMatch ?? Array.Empty<string>())}}}";
    }

    public List<KeyValuePair<string, object?>> ToKeyValuePair()
    {
        List<KeyValuePair<string, object?>> data = new() { new("id", Id) };
        if (OnMatch != null)
        {
            data.Add(new("on_match", OnMatch));
        }

        if (Enabled.HasValue)
        {
            data.Add(new("enabled", Enabled.Value));
        }

        return data;
    }
}
