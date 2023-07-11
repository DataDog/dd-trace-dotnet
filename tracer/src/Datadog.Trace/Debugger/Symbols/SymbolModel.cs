// <copyright file="SymbolModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols
{
    internal enum SymbolType
    {
        Assembly,
        Class,
        Method,
        Field,
        Arg,
        Local
    }

    internal record struct SymbolModel
    {
        [JsonProperty]
        internal string Service { get; set; }

        [JsonProperty]
        internal string Env { get; set; }

        [JsonProperty]
        internal string Version { get; set; }

        [JsonProperty]
        internal string Language { get; set; }

        [JsonProperty]
        internal IReadOnlyList<Scope> Scopes { get; set; }
    }

    internal record struct LanguageSpecifics
    {
        [JsonProperty]
        internal IReadOnlyList<string> AccessModifiers { get; set; }

        [JsonProperty]
        internal IReadOnlyList<string> Annotations { get; set; }

        [JsonProperty]
        internal IReadOnlyList<string> SuperClasses { get; set; }

        [JsonProperty]
        internal IReadOnlyList<string> Interfaces { get; set; }

        [JsonProperty]
        internal IReadOnlyList<string> ReturnType { get; set; }
    }

    internal record struct Symbol
    {
        [JsonProperty]
        internal string Name { get; set; }

        [JsonProperty]
        internal string Type { get; set; }

        [JsonProperty]
        internal SymbolType SymbolType { get; set; }

        [JsonProperty]
        internal int Line { get; set; }
    }

    internal record struct Scope
    {
        [JsonProperty]
        internal SymbolType SymbolType { get; set; }

        [JsonProperty]
        internal string Name { get; set; }

        [JsonProperty]
        internal string Type { get; set; }

        [JsonProperty]
        internal string SourceFile { get; set; }

        [JsonProperty]
        internal int StartLine { get; set; }

        [JsonProperty]
        internal int EndLine { get; set; }

        [JsonProperty]
        internal LanguageSpecifics LanguageSpecifics { get; set; }

        [JsonProperty]
        internal IReadOnlyList<Symbol> Symbols { get; set; }

        [JsonProperty]
        internal IList<Scope> Scopes { get; set; }
    }
}
