// <copyright file="Scope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct Scope
{
    [JsonProperty("scope_type")]
    [JsonConverter(typeof(StringEnumConverter), converterParameters: typeof(SnakeCaseNamingStrategy))]
    internal ScopeType ScopeType { get; set; }

    [JsonProperty("name")]
    internal string? Name { get; set; }

    [JsonProperty("source_file")]
    internal string? SourceFile { get; set; }

    [JsonProperty("start_line")]
    internal int StartLine { get; set; }

    [JsonProperty("end_line")]
    internal int EndLine { get; set; }

    [JsonProperty("language_specifics")]
    internal LanguageSpecifics? LanguageSpecifics { get; set; }

    [JsonProperty("symbols")]
    internal Symbol[]? Symbols { get; set; }

    [JsonProperty("scopes")]
    internal Scope[]? Scopes { get; set; }
}
