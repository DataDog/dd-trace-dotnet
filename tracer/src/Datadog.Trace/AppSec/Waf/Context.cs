// <copyright file="Context.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.DataFormat;
using Datadog.Trace.AppSec.Waf.Rules;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Context : IContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Context>();

        private readonly List<Rule> rules;

        public Context(List<Rule> rules)
        {
            this.rules = rules;
        }

        public IResult Run(Node args)
        {
            var matches = new List<RuleMatch>();
            foreach (var rule in rules)
            {
                if (rule.IsMatch(args))
                {
                    matches.Add(new RuleMatch() { Id = rule.Id, Name = rule.Name });
                }
            }

            var result = matches.Count > 0 ? ReturnCode.Monitor : ReturnCode.Good;
            var ret = new Result(result, matches);

            return ret;
        }
    }
}
