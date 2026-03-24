// <copyright file="FfeProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Rcm;

internal sealed class FfeProduct
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FfeProduct));

    private readonly Action<List<KeyValuePair<string, ServerConfiguration>>> _onNewConfig;
    private List<KeyValuePair<string, ServerConfiguration>> _serverConfigurations = new List<KeyValuePair<string, ServerConfiguration>>();

    public FfeProduct(Action<List<KeyValuePair<string, ServerConfiguration>>> onNewConfig)
    {
        _onNewConfig = onNewConfig;
    }

    public ApplyDetails[] UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
    {
        Log.Debug("FfeProduct::UpdateFromRcm -> Processing new config...");
        List<ApplyDetails> res = new List<ApplyDetails>();
        bool apply = false;

        try
        {
            if (removedConfigsByProduct is not null && removedConfigsByProduct.TryGetValue(RcmProducts.FfeFlags, out var removedConfigs))
            {
                foreach (var removedConfig in removedConfigs)
                {
                    apply |= (_serverConfigurations.RemoveAll((x) => x.Key == removedConfig.Path) > 0);
                }
            }

            if (configsByProduct.TryGetValue(RcmProducts.FfeFlags, out var ffeConfigs))
            {
                foreach (var ffeConfig in ffeConfigs)
                {
                    var serverConfigFile = new NamedRawFile(ffeConfig.Path, ffeConfig.Contents).Deserialize<ServerConfiguration>();
                    if (serverConfigFile.TypedFile is not null)
                    {
                        _serverConfigurations.Add(new KeyValuePair<string, ServerConfiguration>(ffeConfig.Path.Path, serverConfigFile.TypedFile));
                        res.Add(ApplyDetails.FromOk(ffeConfig.Path.Path));
                        apply = true;
                    }
                }
            }

            if (apply)
            {
                _onNewConfig?.Invoke(_serverConfigurations);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while updating FFE RCM config");
        }

        return [.. res];
    }
}
