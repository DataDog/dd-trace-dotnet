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

    internal override void UpdateRemoteConfigurationStatus(List<RemoteConfiguration>? files, List<RemoteConfigurationPath>? removedConfigsForThisProduct, ConfigurationStatus configurationStatus)
    {
        base.UpdateRemoteConfigurationStatus(files, removedConfigsForThisProduct, configurationStatus);
        configurationStatus.EnableAsm = !configurationStatus.AsmFeaturesByFile.IsEmpty() && configurationStatus.AsmFeaturesByFile.All(a => a.Value.Enabled == true);
    }

    protected override void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files)
    {
        foreach (var file in files)
        {
            var asmFeatures = new NamedRawFile(file.Path, file.Contents).Deserialize<AsmFeatures>();
            if (asmFeatures.TypedFile != null)
            {
                configurationStatus.AsmFeaturesByFile[file.Path.Path] = asmFeatures.TypedFile.Asm;
            }

            configurationStatus.IncomingUpdateState.SignalSecurityStateChange();
        }
    }

    protected override void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        foreach (var removedConfig in removedConfigsForThisProduct)
        {
            configurationStatus.AsmFeaturesByFile.Remove(removedConfig.Path);
        }

        configurationStatus.IncomingUpdateState.SignalSecurityStateChange();
    }
}
