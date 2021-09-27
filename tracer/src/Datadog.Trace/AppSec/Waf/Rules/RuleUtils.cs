// <copyright file="RuleUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf.Rules
{
    internal static class RuleUtils
    {
        public static string RemoveMapAccessor(string inputKey)
        {
            var index = inputKey.IndexOf(':');
            return index > 0 ? inputKey.Remove(index) : inputKey;
        }

        public static string MakeTransformInputKey(string transform, string input)
        {
            return $"{transform}-{input}";
        }
    }
}
