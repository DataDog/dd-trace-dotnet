// <copyright file="AsmDataProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmDataProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_DATA";

    internal override List<RemoteConfigurationPath> UpdateRemoteConfigurationStatus(List<RemoteConfiguration>? files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            var removedData = false;
            foreach (var configurationPath in removedConfigsForThisProduct)
            {
                removedData |= configurationStatus.RulesDataByFile.Remove(configurationPath.Path);
            }

            if (removedData)
            {
                configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesDataKey);
            }
        }

        var paths = new List<RemoteConfigurationPath>();
        if (files != null)
        {
            foreach (var file in files)
            {
                paths.Add(file.Path);
                var rawFile = new NamedRawFile(file.Path, file.Contents);
                var asmDataConfig = rawFile.Deserialize<Payload>();
                var rulesData = asmDataConfig.TypedFile!.RulesData;
                if (rulesData != null)
                {
                    configurationStatus.RulesDataByFile[rawFile.Path.Path] = rulesData;
                    configurationStatus.IncomingUpdateState.WafKeysToApply.Add(ConfigurationStatus.WafRulesDataKey);
                }
            }
        }

        return paths;
    }
}
