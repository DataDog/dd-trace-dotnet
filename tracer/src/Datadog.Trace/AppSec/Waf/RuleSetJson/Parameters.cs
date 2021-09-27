// <copyright file="Parameters.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.AppSec.Waf.RuleSetJson
{
    internal class Parameters
    {
        public List<string> Inputs { get; set; }

        public List<string> List { get; set; }

        public string Regex { get; set; }
    }
}
