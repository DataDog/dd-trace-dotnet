// <copyright file="Root.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols.Model
{
    internal enum SymbolType
    {
        Assembly,
        Class,
        Method,
        Closure,
        Field,
        StaticField,
        Arg,
        Local
    }

    internal record Root
    {
        [JsonProperty("service")]
        internal string Service { get; set; }

        [JsonProperty("env")]
        internal string Env { get; set; }

        [JsonProperty("version")]
        internal string Version { get; set; }

        [JsonProperty("language")]
        internal string Language { get; set; }

        [JsonProperty("scopes")]
        internal IReadOnlyList<Scope> Scopes { get; set; }
    }
}
