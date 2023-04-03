// <copyright file="AsmProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM";

    protected override void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files)
    {
        foreach (var file in files)
        {
            var asmConfig = new NamedRawFile(file.Path, file.Contents).Deserialize<Payload>();
            if (asmConfig.TypedFile == null)
            {
                continue;
            }

            if (asmConfig.TypedFile.RuleOverrides != null)
            {
                configurationStatus.RulesOverridesByFile[asmConfig.Name] = asmConfig.TypedFile.RuleOverrides;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
            }

            if (asmConfig.TypedFile.Exclusions != null)
            {
                configurationStatus.ExclusionsByFile[asmConfig.Name] = asmConfig.TypedFile.Exclusions;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafExclusionsKey);
            }

            if (asmConfig.TypedFile.Actions != null)
            {
                foreach (var action in asmConfig.TypedFile.Actions)
                {
                    if (action.Id is not null)
                    {
                        configurationStatus.Actions[action.Id] = action;
                    }
                }

                if (asmConfig.TypedFile.Actions.Length == 0)
                {
                    configurationStatus.Actions.Clear();
                }
            }
        }
    }

    protected override void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        var removedRulesOveride = false;
        var removedExclusions = false;
        foreach (var configurationPath in removedConfigsForThisProduct)
        {
            removedRulesOveride |= configurationStatus.RulesOverridesByFile.Remove(configurationPath.Path);
            removedExclusions |= configurationStatus.ExclusionsByFile.Remove(configurationPath.Path);
        }

        if (removedRulesOveride)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
        }

        if (removedExclusions)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafExclusionsKey);
        }
    }
}
