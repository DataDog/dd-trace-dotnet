// <copyright file="DdWafMetricsStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal struct DdWafMetricsStruct
    {
        /// <summary>
        /// Total WAF runtime in nanoseconds
        /// </summary>
        public ulong TotalRuntime;

        /// <summary>
        ///  Map containing runtime in nanoseconds per rule
        /// </summary>
        public DdwafObjectStruct RuleRuntime;
    }
}
