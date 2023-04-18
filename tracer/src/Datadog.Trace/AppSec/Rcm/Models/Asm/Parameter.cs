// <copyright file="Parameter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Rcm.Models.Asm;

internal class Parameter
{
    [JsonProperty("status_code")]
    public int StatusCode { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("location")]
    public string? Location { get; set; }
}
