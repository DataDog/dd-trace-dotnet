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

internal class AsmDdProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_DD";

    internal override List<RemoteConfigurationPath> UpdateRemoteConfigurationStatus(List<RemoteConfiguration>? files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            var oneRemoved = false;
            foreach (var removedConfig in removedConfigsForThisProduct)
            {
                oneRemoved |= configurationStatus.RulesByFile.Remove(removedConfig.Path);
            }

            if (configurationStatus.RulesByFile.Count == 0)
            {
                configurationStatus.IncomingUpdateState.FallbackToEmbeddedRuleset();
            }
            else if (oneRemoved)
            {
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesKey);
            }
        }

        var paths = new List<RemoteConfigurationPath>();

        if (files?.Count > 0)
        {
            var firstFile = files.First();
            paths.Add(firstFile.Path);
            var asmDd = new NamedRawFile(firstFile!.Path, firstFile.Contents);
            var result = asmDd.Deserialize<JToken>();
            if (result.TypedFile != null)
            {
                var ruleSet = RuleSet.From(result.TypedFile);
                configurationStatus.RulesByFile[result.TypedFile.Path] = ruleSet;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesKey);
            }
        }

        return paths;
    }
}
