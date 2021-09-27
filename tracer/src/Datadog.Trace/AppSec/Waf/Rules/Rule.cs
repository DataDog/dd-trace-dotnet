// <copyright file="Rule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.DataFormat;
using Datadog.Trace.AppSec.Waf.RuleSetJson;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.Rules
{
    internal class Rule
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RegexMatchCondition));

        private readonly List<ICondition> conditions;
        private readonly List<string> transformations;
        private readonly HashSet<string> inputSet;

        public Rule(string id, string name, List<Condition> conditions, List<string> transformations)
        {
            Id = id;
            Name = name;
            this.transformations = transformations;
            var allInputs = conditions.SelectMany(x => x.Parameters.Inputs);
            inputSet = new HashSet<string>(allInputs);

            this.conditions = new List<ICondition>();
            foreach (var condition in conditions)
            {
                switch (condition.Operation)
                {
                    case "phrase_match":
                        var phraseMatch = new PhraseMatchCondition(condition.Parameters.Inputs, condition.Parameters.List);
                        this.conditions.Add(phraseMatch);
                        break;
                    case "match_regex":
                        var regexMatch = new RegexMatchCondition(condition.Parameters.Inputs, condition.Parameters.Regex);
                        this.conditions.Add(regexMatch);
                        break;
                    default:
                        Log.Warning("Ignoring unknown value: {Value}", condition.Operation);
                        break;
                }
            }
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public bool IsMatch(Node data)
        {
            if (data.Type != NodeType.Map)
            {
                throw new ArgumentException("Top level object must be a map", nameof(data));
            }

            if (conditions.Any(condition => condition.IsMatch(data)))
            {
                return true;
            }

            foreach (var transformation in transformations)
            {
                foreach (var input in inputSet)
                {
                    var transformInputKey = RuleUtils.MakeTransformInputKey(transformation, input);
                    if (!data.MapValue.ContainsKey(transformInputKey) && data.MapValue.TryGetValue(input, out var targetNode))
                    {
                        var transformedNode = Transforms.Transform(targetNode, transformation);
                        // naughty, naughty, very naughty
                        ((Dictionary<string, Node>)data.MapValue).Add(transformInputKey, transformedNode);
                    }
                }

                if (conditions.Any(condition => condition.IsTransformedMatch(data, transformation)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
