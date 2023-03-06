// <copyright file="RemoteConfigurationStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels.Asm;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.RcmModels;

internal class RemoteConfigurationStatus
{
    internal Dictionary<string, List<RuleOverride>> RulesOverridesByFile { get; } = new();

    internal List<RuleOverride> RulesOverrides
    {
        get { return RulesOverridesByFile.SelectMany(x => x.Value).ToList(); }
    }

    internal Dictionary<string, List<RuleData>> RulesDataByFile { get; } = new();

    internal List<RuleData> RulesData
    {
        get { return RulesDataByFile.SelectMany(x => x.Value).ToList(); }
    }

    internal Dictionary<string, List<JToken>> ExclusionsByFile { get; } = new();

    internal List<JToken> Exclusions
    {
        get { return ExclusionsByFile.SelectMany(x => x.Value).ToList(); }
    }

    internal IDictionary<string, Action> Actions { get; set; } = new Dictionary<string, Action>();

    internal string RemoteRulesJson { get; set; }
}
