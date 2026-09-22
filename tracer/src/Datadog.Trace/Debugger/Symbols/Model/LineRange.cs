// <copyright file="LineRange.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct LineRange
{
    [JsonProperty("start")]
    internal int Start { get; set; }

    [JsonProperty("end")]
    internal int End { get; set; }
}
