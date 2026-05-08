// <copyright file="AsmDdProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Rcm;

internal sealed class AsmDdProduct : IAsmConfigUpdater
{
    internal const string DefaultConfigKey = "datadog/00/ASM_DD/default/config";

    public void ProcessUpdates(ConfigurationState configurationStatus, List<RemoteConfigurationPath>? removedConfigs, List<RemoteConfiguration>? files)
    {
        if (removedConfigs is { Count: > 0 })
        {
            foreach (var removedConfig in removedConfigs)
            {
                configurationStatus.RulesetConfigs.RemoveAll(p => p.Key == removedConfig.Path);
            }
        }

        if (files is { Count: > 0 })
        {
            // var file = files.First();
            foreach (var file in files)
            {
                var asmDd = new NamedRawFile(file!.Path, file.Contents);
                var result = asmDd.Deserialize<JToken>();

                if (result.TypedFile != null)
                {
                    RuleSet ruleSet;
                    if (!result.TypedFile.HasValues)
                    {
                        var o = JsonHelper.ParseJObject(result.TypedFile!.Value<string>() ?? string.Empty);
                        ruleSet = RuleSet.From(o);
                    }
                    else
                    {
                        ruleSet = RuleSet.From(result.TypedFile);
                    }

                    configurationStatus.RulesetConfigs.RemoveAll(p => p.Key == file.Path.Path);
                    configurationStatus.RulesetConfigs.Add(new KeyValuePair<string, RuleSet>(file.Path.Path, ruleSet));
                }
            }
        }
    }
}
