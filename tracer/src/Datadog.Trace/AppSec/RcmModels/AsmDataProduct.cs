// <copyright file="AsmDataProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.AppSec.RcmModels;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec;

internal class AsmDataProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_DATA";

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            foreach (var configurationPath in removedConfigsForThisProduct)
            {
                if (configurationStatus.RulesDataByFile.ContainsKey(configurationPath.Path))
                {
                    configurationStatus.RulesDataByFile.Remove(configurationPath.Path);
                }
            }
        }

        foreach (var file in files)
        {
            var rawFile = new NamedRawFile(file.Path, file.Contents);
            var asmDataConfig = rawFile.Deserialize<RcmModels.AsmData.Payload>();
            var rulesData = asmDataConfig.TypedFile!.RulesData;
            if (rulesData != null)
            {
                configurationStatus.RulesDataByFile[rawFile.Path.Path] = rulesData;
            }
        }
    }
}
