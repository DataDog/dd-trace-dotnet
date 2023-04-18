// <copyright file="AsmFeaturesProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmFeaturesProduct : IAsmConfigUpdater
{
    public void ProcessUpdates(ConfigurationStatus configurationStatus, List<RemoteConfiguration> files)
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

    public void ProcessRemovals(ConfigurationStatus configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        foreach (var removedConfig in removedConfigsForThisProduct)
        {
            configurationStatus.AsmFeaturesByFile.Remove(removedConfig.Path);
        }

        configurationStatus.IncomingUpdateState.SignalSecurityStateChange();
    }
}
