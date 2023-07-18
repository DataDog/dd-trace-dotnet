// <copyright file="Symbol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct Symbol
{
    [JsonProperty("name")]
    internal string Name { get; set; }

    [JsonProperty("type")]
    internal string Type { get; set; }

    [JsonProperty("symbolType")]
    [JsonConverter(typeof(StringEnumConverter))]
    internal SymbolType SymbolType { get; set; }

    [JsonProperty("line")]
    internal int Line { get; set; }
}
