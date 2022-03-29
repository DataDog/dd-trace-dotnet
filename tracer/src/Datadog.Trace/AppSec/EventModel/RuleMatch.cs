// <copyright file="RuleMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class RuleMatch
    {
        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("operator_value")]
        public string OperatorValue { get; set; }

        [JsonProperty("parameters")]
        public Parameter[] Parameters { get; set; }

        [JsonProperty("highlight")]
        public string[] Highlight { get; set; }

        [JsonProperty("has_server_side_match")]
        public bool HasServerSideMatch { get; set; }
    }
}
