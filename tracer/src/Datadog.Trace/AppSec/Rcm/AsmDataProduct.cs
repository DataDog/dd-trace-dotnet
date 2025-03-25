// <copyright file="AsmDataProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm;

internal class AsmDataProduct : IAsmConfigUpdater
{
    public void ProcessUpdates(ConfigurationState configurationStatus, List<RemoteConfiguration> files)
    {
        foreach (var file in files)
        {
            var payload = new NamedRawFile(file.Path, file.Contents).Deserialize<JToken>();
            if (payload.TypedFile == null)
            {
                continue;
            }

            var asmConfig = payload.TypedFile;
            var asmConfigName = payload.Name;

            configurationStatus.AsmDataConfigs[asmConfigName] = asmConfig;
        }
    }

    public void ProcessRemovals(ConfigurationState configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        foreach (var configurationPath in removedConfigsForThisProduct)
        {
            configurationStatus.AsmDataConfigs.Remove(configurationPath.Path);
        }
    }
}
