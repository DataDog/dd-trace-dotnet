// <copyright file="AsmDDProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.AppSec.RcmModels.AsmDd;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec;

internal class AsmDdProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_DD";

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (files.Count > 0)
        {
            var firstFile = files.First();
            var asmDd = new NamedRawFile(firstFile!.Path, firstFile.Contents);
            var result = asmDd.Deserialize<JToken>();
            if (result.TypedFile != null)
            {
                var ruleSet = RuleSet.From(result.TypedFile);
                configurationStatus.RulesByFile[result.TypedFile.Path] = ruleSet;
            }
        }

        if (removedConfigsForThisProduct != null)
        {
            foreach (var removedConfig in removedConfigsForThisProduct)
            {
                configurationStatus.RulesByFile.Remove(removedConfig.Path);
            }

            if (configurationStatus.RulesByFile.Count == 0)
            {
                configurationStatus.FallbackToEmbeddedRulesetAtNextUpdate = true;
            }
        }
    }
}
