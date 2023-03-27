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

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration> files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, RemoteConfigurationStatus remoteConfigurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            foreach (var configurationPath in removedConfigsForThisProduct)
            {
                if (remoteConfigurationStatus.RulesDataByFile.ContainsKey(configurationPath.Path))
                {
                    remoteConfigurationStatus.RulesDataByFile.Remove(configurationPath.Path);
                }
            }
        }

        foreach (var file in files)
        {
            var rawFile = new NamedRawFile(file.Path, file.Contents);
            var asmDataConfig = rawFile.Deserialize<RcmModels.AsmData.Payload>();
            remoteConfigurationStatus.RulesDataByFile[rawFile.Path.Path] = asmDataConfig.TypedFile!.RulesData;
        }
    }
}
