// <copyright file="LanguageSpecifics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Symbols.Model;

internal record struct LanguageSpecifics
{
    [JsonProperty("accessModifiers")]
    internal IReadOnlyList<string> AccessModifiers { get; set; }

    [JsonProperty("annotations")]
    internal IReadOnlyList<string> Annotations { get; set; }

    [JsonProperty("superClasses")]
    internal IReadOnlyList<string> SuperClasses { get; set; }

    [JsonProperty("interfaces")]
    internal IReadOnlyList<string> Interfaces { get; set; }

    [JsonProperty("returnType")]
    internal string ReturnType { get; set; }

    [JsonProperty("startColumn")]
    internal int? StartColumn { get; set; }

    [JsonProperty("endColumn")]
    internal int? EndColumn { get; set; }
}
