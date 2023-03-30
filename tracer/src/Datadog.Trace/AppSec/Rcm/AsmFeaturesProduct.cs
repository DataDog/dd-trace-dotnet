// <copyright file="AsmFeaturesProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmFeaturesProduct : AsmRemoteConfigurationProduct
{
    public override string Name => "ASM_FEATURES";

    internal override List<RemoteConfigurationPath> UpdateRemoteConfigurationStatus(List<RemoteConfiguration>? files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        if (removedConfigsForThisProduct != null)
        {
            foreach (var removedConfig in removedConfigsForThisProduct)
            {
                configurationStatus.AsmFeaturesByFile.Remove(removedConfig.Path);
            }

            configurationStatus.IncomingUpdateState.SignalSecurityStateChange();
        }

        var paths = new List<RemoteConfigurationPath>();
        if (files != null)
        {
            foreach (var file in files)
            {
                paths.Add(file.Path);
                var asmFeatures = new NamedRawFile(file.Path, file.Contents).Deserialize<AsmFeatures>();
                if (asmFeatures.TypedFile != null)
                {
                    configurationStatus.AsmFeaturesByFile[file.Path.Path] = asmFeatures.TypedFile.Asm;
                }

                configurationStatus.IncomingUpdateState.SignalSecurityStateChange();
            }
        }

        configurationStatus.EnableAsm = !configurationStatus.AsmFeaturesByFile.IsEmpty() && configurationStatus.AsmFeaturesByFile.All(a => a.Value.Enabled == true);
        return paths;
    }
}
