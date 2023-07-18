// <copyright file="Scope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct Scope
{
    [JsonProperty("scopeType")]
    [JsonConverter(typeof(StringEnumConverter))]
    internal SymbolType ScopeType { get; set; }

    [JsonProperty("name")]
    internal string Name { get; set; }

    [JsonProperty("type")]
    internal string Type { get; set; }

    [JsonProperty("sourceFile")]
    internal string SourceFile { get; set; }

    [JsonProperty("startLine")]
    internal int StartLine { get; set; }

    [JsonProperty("endLine")]
    internal int EndLine { get; set; }

    [JsonProperty("languageSpecifics")]
    internal LanguageSpecifics? LanguageSpecifics { get; set; }

    [JsonProperty("symbols")]
    internal IReadOnlyList<Symbol> Symbols { get; set; }

    [JsonProperty("scopes")]
    internal IList<Scope> Scopes { get; set; }
}
