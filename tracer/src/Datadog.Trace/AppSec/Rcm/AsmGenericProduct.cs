// <copyright file="AsmGenericProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm;

internal sealed class AsmGenericProduct : IAsmConfigUpdater
{
    private readonly Func<Dictionary<string, JToken>> _getCollection;

    public AsmGenericProduct(Func<Dictionary<string, JToken>> getCollection)
    {
        _getCollection = getCollection;
    }

    public void ProcessUpdates(ConfigurationState configurationStatus, List<RemoteConfigurationPath>? removedConfigs, List<RemoteConfiguration>? files)
    {
        var collection = _getCollection();

        if (removedConfigs is { Count: > 0 })
        {
            foreach (var configurationPath in removedConfigs)
            {
                collection.Remove(configurationPath.Path);
            }
        }

        if (files is { Count: > 0 })
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

                collection[asmConfigName] = asmConfig;
            }
        }
    }

    public void ProcessRemovals(ConfigurationState configurationStatus, List<RemoteConfigurationPath> removedConfigsForThisProduct)
    {
        var collection = _getCollection();
        foreach (var configurationPath in removedConfigsForThisProduct)
        {
            collection.Remove(configurationPath.Path);
        }
    }
}
