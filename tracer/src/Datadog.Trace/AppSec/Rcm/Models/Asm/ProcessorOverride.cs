// <copyright file="ProcessorOverride.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm.Models.Asm;

internal class ProcessorOverride
{
    [JsonProperty("scanners")]
    public JToken? Scanners { get; set; }

    [JsonProperty("target")]
    public string[]? Target { get; set; }

    public override string ToString() => $"{{target: {string.Join(",", Target ?? Array.Empty<string>())}, scanners: {Scanners}}}";

    public List<KeyValuePair<string, object?>> ToKeyValuePair()
    {
        List<KeyValuePair<string, object?>> data = new();
        if (Target is not null)
        {
            data.Add(new("target", Target));
        }

        if (Scanners is not null)
        {
            data.Add(new("scanners", Scanners));
        }

        return data;
    }
}
