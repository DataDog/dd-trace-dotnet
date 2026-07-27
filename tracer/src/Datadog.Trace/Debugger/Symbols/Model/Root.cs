// <copyright file="Root.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols.Model
{
    internal enum SymbolType
    {
        Field,
        StaticField,
        Arg,
        Local
    }

    internal enum ScopeType
    {
        Assembly,
        Class,
        Method,
        Closure,
        Local
    }

    internal sealed record Root
    {
        [JsonProperty("service")]
        internal string? Service { get; set; }

        [JsonProperty("env")]
        internal string? Env { get; set; }

        [JsonProperty("version")]
        internal string? Version { get; set; }

        [JsonProperty("language")]
        internal string? Language { get; set; }

        [JsonProperty("scopes")]
        internal IReadOnlyList<Scope>? Scopes { get; set; }

        // upload_id/batch_num/final use snake_case to match the rest of the
        // attachment scope schema (scope_type, source_file, etc.).
        [JsonProperty("upload_id")]
        internal Guid? UploadId { get; set; }

        [JsonProperty("batch_num")]
        internal long? BatchNum { get; set; }

        [JsonProperty("final")]
        internal bool? Final { get; set; }
    }
}
