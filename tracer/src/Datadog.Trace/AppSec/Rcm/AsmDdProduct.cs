// <copyright file="AsmDdProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmDdProduct : IAsmConfigUpdater
{
    public void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files)
    {
        var firstFile = files.First();
        var asmDd = new NamedRawFile(firstFile!.Path, firstFile.Contents);
        var result = asmDd.Deserialize<JToken>();

        if (result.TypedFile != null)
        {
            RuleSet ruleSet;
            if (!result.TypedFile.HasValues)
            {
                var o = JObject.Parse(result.TypedFile!.Value<string>() ?? string.Empty);
                ruleSet = RuleSet.From(o);
            }
            else
            {
                ruleSet = RuleSet.From(result.TypedFile);
            }

            configurationStatus.RulesByFile[firstFile.Path.Path] = ruleSet;
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesKey);
        }
    }

    public void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        foreach (var removedConfig in removedConfigsForThisProduct)
        {
            configurationStatus.RulesByFile.Remove(removedConfig.Path);
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesKey);
        }
    }
}
