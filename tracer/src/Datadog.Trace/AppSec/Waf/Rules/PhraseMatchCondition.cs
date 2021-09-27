// <copyright file="PhraseMatchCondition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.DataFormat;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.Rules
{
    internal class PhraseMatchCondition : ICondition
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PhraseMatchCondition));

        private readonly List<string> inputs;
        private readonly List<string> patterns;

        public PhraseMatchCondition(List<string> inputs, List<string> patterns)
        {
            this.inputs = inputs;
            this.patterns = patterns;
        }

        public bool IsMatch(Node data)
        {
            if (data.Type != NodeType.Map)
            {
                throw new ArgumentException("Top level object must be a map", nameof(data));
            }

            return IsMatchInternal(data);
        }

        private bool IsMatchInternal(Node data, string transformKey = null)
        {
            foreach (var input in inputs)
            {
                var key = transformKey == null ? input : RuleUtils.MakeTransformInputKey(transformKey, input);
                if (data.MapValue.TryGetValue(key, out var currentValue))
                {
                    return Visitor.DepthFirstSearch(data, stringNodeValue => patterns.Any(stringNodeValue.Contains));
                }
            }

            return false;
        }

        public bool IsTransformedMatch(Node data, string transformation)
        {
            if (data.Type != NodeType.Map)
            {
                throw new ArgumentException("Top level object must be a map", nameof(data));
            }

            return IsMatchInternal(data, transformation);
        }
    }
}
