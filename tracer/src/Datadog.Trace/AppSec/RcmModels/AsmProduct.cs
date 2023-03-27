// <copyright file="AsmProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec;

internal class AsmProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM";

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, RemoteConfigurationStatus remoteConfigurationStatus)
    {
        var asmConfigs = files.Select(configContent => new NamedRawFile(configContent.Path, configContent.Contents).Deserialize<RcmModels.Asm.Payload>());
        if (removedConfigsForThisProduct != null)
        {
            foreach (var configurationPath in removedConfigsForThisProduct)
            {
                if (remoteConfigurationStatus.RulesOverridesByFile.ContainsKey(configurationPath.Path))
                {
                    remoteConfigurationStatus.RulesOverridesByFile.Remove(configurationPath.Path);
                }

                if (remoteConfigurationStatus.ExclusionsByFile.ContainsKey(configurationPath.Path))
                {
                    remoteConfigurationStatus.ExclusionsByFile.Remove(configurationPath.Path);
                }

                // todo actions by file ?
            }
        }

        foreach (var asmConfig in asmConfigs)
        {
            if (asmConfig.TypedFile == null)
            {
                continue;
            }

            if (asmConfig.TypedFile.RuleOverrides != null)
            {
                remoteConfigurationStatus.RulesOverridesByFile[asmConfig.Name] = asmConfig.TypedFile.RuleOverrides;
            }

            if (asmConfig.TypedFile.Exclusions != null)
            {
                remoteConfigurationStatus.ExclusionsByFile[asmConfig.Name] = asmConfig.TypedFile.Exclusions;
            }

            if (asmConfig.TypedFile.Actions != null)
            {
                foreach (var action in asmConfig.TypedFile.Actions)
                {
                    if (action.Id is not null)
                    {
                        remoteConfigurationStatus.Actions[action.Id] = action;
                    }
                }

                if (asmConfig.TypedFile.Actions.Length == 0)
                {
                    remoteConfigurationStatus.Actions.Clear();
                }
            }
        }
    }
}
