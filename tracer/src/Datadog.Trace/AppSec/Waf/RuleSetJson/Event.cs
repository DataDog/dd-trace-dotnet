// <copyright file="Event.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.Rules;

namespace Datadog.Trace.AppSec.Waf.RuleSetJson
{
    internal class Event
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public Dictionary<string, string> Tags { get; set; }

        public List<Condition> Conditions { get; set; }

        public Parameters Parameters { get; set; }

        public List<string> Transformers { get; set; }
    }
}
