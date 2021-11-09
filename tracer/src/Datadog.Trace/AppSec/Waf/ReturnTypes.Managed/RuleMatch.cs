// <copyright file="RuleMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class RuleMatch
    {
        /// <summary>
        /// Gets or sets the rule operator that triggered this event. For example, ``match_regex`` or
        /// ``phrase_match``.
        /// </summary>
        [JsonProperty("operator")]
        public string Operator { get; set; }

        /// <summary>
        /// Gets or sets the rule operator operand that triggered this event. For example, the word that triggered
        /// using the ``phrase_match`` operator.
        /// </summary>
        [JsonProperty("operator_value")]
        public string OperatorValue { get; set; }

        [JsonProperty("parameters")]
        public Parameter[] Parameters { get; set; }
    }
}
