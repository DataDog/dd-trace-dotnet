// <copyright file="RuleOverride.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.RcmModels.Asm;

internal class RuleOverride
{
    public string? Id { get; set; }

    public bool? Enabled { get; set; }

    [JsonProperty("on_match")]
    public List<string>? OnMatch { get; set; } = new List<string>();

    public override string ToString()
    {
        return $"{{{Id} : Enabled: {Enabled}, OnMatch: {string.Join(", ", OnMatch ?? Enumerable.Empty<string>())} }}";
    }
}
