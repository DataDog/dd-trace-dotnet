// <copyright file="EncoderExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec;

internal static class EncoderExtensions
{
    internal static Obj Encode(this List<RuleData> ruleData, WafLibraryInvoker wafLibraryInvoker, List<Obj> argsToDispose)
    {
        var dictionary = new Dictionary<string, object> { { "rules_data", ruleData.Select(r => r.ToKeyValuePair()).ToArray() } };
        return Encoder.Encode(dictionary, wafLibraryInvoker, argsToDispose, false);
    }

    internal static Obj Encode(List<RuleOverride> ruleStatus, List<JToken> exclusions, WafLibraryInvoker wafLibraryInvoker, List<Obj> argsToDispose)
    {
        var dictionary = new Dictionary<string, object>
        {
            { "rules_override", ruleStatus.Select(r => r.ToKeyValuePair()).ToArray() },
            { "exclusions", new JArray(exclusions) },
        };
        return Encoder.Encode(dictionary, wafLibraryInvoker, argsToDispose);
    }
}
