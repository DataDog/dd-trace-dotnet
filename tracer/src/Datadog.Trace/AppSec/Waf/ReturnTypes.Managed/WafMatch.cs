// <copyright file="WafMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class WafMatch
    {
        [JsonProperty("rule")]
        internal Rule Rule { get; set; }

        [JsonProperty("rule_matches")]
        public RuleMatch[] RuleMatches { get; set; }

        [JsonProperty("span_id", NullValueHandling = NullValueHandling.Ignore)]
        internal ulong? SpanId { get; set; }
    }
}
