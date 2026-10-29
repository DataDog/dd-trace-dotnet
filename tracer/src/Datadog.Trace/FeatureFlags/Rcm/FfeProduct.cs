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
    private readonly Action<List<KeyValuePair<string, ServerConfiguration>>> _onNewConfig;
    private readonly IDatadogLogger _log;
    private List<KeyValuePair<string, ServerConfiguration>> _serverConfigurations = new List<KeyValuePair<string, ServerConfiguration>>();

    public FfeProduct(Action<List<KeyValuePair<string, ServerConfiguration>>> onNewConfig, IDatadogLogger? logger = null)
    {
        _onNewConfig = onNewConfig;
        _log = logger ?? DatadogLogging.GetLoggerFor(typeof(FfeProduct));
    }

    public ApplyDetails[] UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
    {
        _log.Debug("FfeProduct::UpdateFromRcm -> Processing new config...");
        List<ApplyDetails> res = new List<ApplyDetails>();
        bool apply = false;

        try
        {
            if (removedConfigsByProduct is not null && removedConfigsByProduct.TryGetValue(RcmProducts.FfeFlags, out var removedConfigs))
            {
                foreach (var removedConfig in removedConfigs)
                {
                    var removed = _serverConfigurations.RemoveAll((x) => x.Key == removedConfig.Path) > 0;
                    apply |= removed;
                    if (removed)
                    {
                        _log.Debug<string>("Feature Flags: remote_config configuration file {Path} removed", removedConfig.Path);
                    }
                }
            }

            if (configsByProduct.TryGetValue(RcmProducts.FfeFlags, out var ffeConfigs))
            {
                foreach (var ffeConfig in ffeConfigs)
                {
                    _log.Debug<string, long, long>(
                        "Feature Flags: remote_config configuration file {Path} received (version {Version}, {Length} bytes)",
                        ffeConfig.Path.Path,
                        ffeConfig.Version,
                        ffeConfig.Length);
                    var serverConfigFile = new NamedRawFile(ffeConfig.Path, ffeConfig.Contents).Deserialize<ServerConfiguration>();
                    if (serverConfigFile.TypedFile is null)
                    {
                        // Not acknowledged either, so the file is not reported as applied anywhere.
                        _log.Debug<string>("Feature Flags: remote_config configuration file {Path} was rejected: it holds no configuration, so the version of it applied before, if any, stays in use", ffeConfig.Path.Path);
                    }
                    else
                    {
                        // A modified file arrives under the path it already has, and never in the
                        // removed set. The configurations are merged, and a merge cannot delete a
                        // flag, so a stale version left here keeps serving flags the new one dropped.
                        _serverConfigurations.RemoveAll(x => x.Key == ffeConfig.Path.Path);
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
            _log.Error(ex, "Error while updating FFE RCM config");
        }

        return [.. res];
    }
}
