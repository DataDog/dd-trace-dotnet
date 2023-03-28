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
    internal Dictionary<string, RuleOverride[]> RulesOverridesByFile { get; } = new();

    internal Dictionary<string, RuleData[]> RulesDataByFile { get; } = new();

    internal Dictionary<string, JArray> ExclusionsByFile { get; } = new();

    internal IDictionary<string, Action> Actions { get; set; } = new Dictionary<string, Action>();

    internal Dictionary<string, object> CustomAttributes { get; set; } = new();

    internal string RemoteRulesJson { get; set; }
}
