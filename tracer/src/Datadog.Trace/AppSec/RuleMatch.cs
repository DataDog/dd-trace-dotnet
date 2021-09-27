// <copyright file="RuleMatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec
{
    internal class RuleMatch
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Operator { get; set; }

        public string BindingAccessor { get; set; }

        public string ResolvedValue { get; set; }
    }
}
