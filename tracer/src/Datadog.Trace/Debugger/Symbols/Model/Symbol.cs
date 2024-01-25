// <copyright file="Symbol.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct Symbol
{
    [JsonProperty("name")]
    internal string Name { get; set; }

    [JsonProperty("type")]
    internal string Type { get; set; }

    [JsonProperty("symbol_type")]
    [JsonConverter(typeof(StringEnumConverter), converterParameters: typeof(SnakeCaseNamingStrategy))]
    internal SymbolType SymbolType { get; set; }

    [JsonProperty("line")]
    internal int Line { get; set; }

    [JsonProperty("language_specifics")]
    internal LanguageSpecifics? LanguageSpecifics { get; set; }
}
