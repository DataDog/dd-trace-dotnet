// <copyright file="AsmProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmProduct : IAsmConfigUpdater
{
    public void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files)
    {
        foreach (var file in files)
        {
            var payload = new NamedRawFile(file.Path, file.Contents).Deserialize<Payload>();
            if (payload.TypedFile == null)
            {
                continue;
            }

            var asmConfig = payload.TypedFile;
            var asmConfigName = payload.Name;

            if (asmConfig.RuleOverrides != null)
            {
                configurationStatus.RulesOverridesByFile[asmConfigName] = asmConfig.RuleOverrides;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
            }

            if (asmConfig.Exclusions != null)
            {
                configurationStatus.ExclusionsByFile[asmConfigName] = asmConfig.Exclusions;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafExclusionsKey);
            }

            if (asmConfig.CustomRules != null)
            {
                configurationStatus.CustomRulesByFile[asmConfigName] = asmConfig.CustomRules;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafCustomRulesKey);
            }

            if (asmConfig.Actions != null)
            {
                configurationStatus.ActionsByFile[asmConfigName] = asmConfig.Actions;
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
            }
        }
    }

    public void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        var removedRulesOverride = false;
        var removedExclusions = false;
        var removedCustomRules = false;
        var removedActions = false;
        foreach (var configurationPath in removedConfigsForThisProduct)
        {
            removedRulesOverride |= configurationStatus.RulesOverridesByFile.Remove(configurationPath.Path);
            removedExclusions |= configurationStatus.ExclusionsByFile.Remove(configurationPath.Path);
            removedCustomRules |= configurationStatus.CustomRulesByFile.Remove(configurationPath.Path);
            removedActions |= configurationStatus.ActionsByFile.Remove(configurationPath.Path);
        }

        if (removedRulesOverride)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesOverridesKey);
        }

        if (removedExclusions)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafExclusionsKey);
        }

        if (removedCustomRules)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafCustomRulesKey);
        }

        if (removedActions)
        {
            configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafActionsKey);
        }
    }
}
