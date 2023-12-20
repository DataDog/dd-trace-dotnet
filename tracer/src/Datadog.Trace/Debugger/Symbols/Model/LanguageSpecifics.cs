// <copyright file="LanguageSpecifics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct LanguageSpecifics
{
    [JsonProperty("access_modifiers")]
    internal IReadOnlyList<string>? AccessModifiers { get; set; }

    [JsonProperty("annotations")]
    internal IReadOnlyList<string>? Annotations { get; set; }

    [JsonProperty("super_classes")]
    internal IReadOnlyList<string>? SuperClasses { get; set; }

    [JsonProperty("interfaces")]
    internal IReadOnlyList<string>? Interfaces { get; set; }

    [JsonProperty("return_type")]
    internal string? ReturnType { get; set; }

    [JsonProperty("start_column")]
    internal int? StartColumn { get; set; }

    [JsonProperty("end_column")]
    internal int? EndColumn { get; set; }

    [JsonProperty("pdb_exist")]
    internal bool? IsPdbExist { get; set; }
}
