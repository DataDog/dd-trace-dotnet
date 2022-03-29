// <copyright file="Filter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class Filter
    {
        [JsonProperty("operator")]
        internal string Operator { get; set; }

        [JsonProperty("operator_value")]
        internal string OperatorValue { get; set; }

        [JsonProperty("binding_accessor")]
        internal string BindingAccessor { get; set; }

        [JsonProperty("manifest_key")]
        internal string ManifestKey { get; set; }

        [JsonProperty("resolved_value")]
        internal string ResolvedValue { get; set; }

        [JsonProperty("match_status")]
        internal string MatchStatus { get; set; }
    }
}
