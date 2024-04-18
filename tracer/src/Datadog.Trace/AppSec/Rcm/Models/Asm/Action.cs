// <copyright file="Action.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Rcm.Models.Asm;

internal class Action
{
    public string? Id { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("parameters")]
    public Parameter? Parameters { get; set; }

    public List<KeyValuePair<string, object?>> ToKeyValuePair() => new() { new("type", Type), new("id", Id), new("parameters", Parameters?.ToKeyValuePair()) };
}
