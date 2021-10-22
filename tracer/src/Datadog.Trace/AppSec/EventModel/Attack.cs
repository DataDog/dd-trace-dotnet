// <copyright file="Attack.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.AppSec.Transport;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.EventModel
{
    internal class Attack : AppSecEvent
    {
        [JsonProperty("event_type")]
        public const string EventType = "appsec.threat.attack";

        [JsonProperty("rule")]
        public Rule Rule { get; set; }

        [JsonProperty("rule_matches")]
        public RuleMatch[] RuleMatches { get; set; }

        [JsonProperty("context")]
        public Context Context { get; set; }

        public static Attack From(ResultData resultData, bool blocked, Trace.Span span, ITransport transport)
        {
            var ruleMatches = resultData.Filter.Select(ruleMatch => new RuleMatch
            {
                Operator = ruleMatch.Operator,
                OperatorValue = ruleMatch.OperatorValue,
                Highlight = new string[] { ruleMatch.MatchStatus },
                Parameters = new Parameter[] { new Parameter { Name = ruleMatch.BindingAccessor, Value = ruleMatch.ResolvedValue } }
            }).ToArray();

            var frameworkDescription = FrameworkDescription.Instance;
            var attack = new Attack
            {
                Rule = new Rule { Name = resultData.Flow, Id = resultData.Rule },
                RuleMatches = ruleMatches,
                Blocked = blocked,
            };

            return attack;
        }
    }
}
