// <copyright file="FfeProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm;

internal sealed class FfeProduct
{
    private readonly Func<List<KeyValuePair<string, ServerConfiguration>>, bool> _tryApplyConfigurations;
    private readonly IDatadogLogger _log;
    private readonly HashSet<string> _pendingWithdrawals = [];
    private List<KeyValuePair<string, ServerConfiguration>> _serverConfigurations = [];

    public FfeProduct(Func<List<KeyValuePair<string, ServerConfiguration>>, bool> tryApplyConfigurations, IDatadogLogger? logger = null)
    {
        _tryApplyConfigurations = tryApplyConfigurations;
        _log = logger ?? DatadogLogging.GetLoggerFor(typeof(FfeProduct));
    }

    public ApplyDetails[] UpdateFromRcm(Dictionary<string, List<RemoteConfiguration>> configsByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
    {
        _log.Debug("FfeProduct::UpdateFromRcm -> Processing new config...");
        configsByProduct.TryGetValue(RcmProducts.FfeFlags, out var files);
        List<RemoteConfigurationPath>? removals = null;
        removedConfigsByProduct?.TryGetValue(RcmProducts.FfeFlags, out removals);
        if (files is not { Count: > 0 } && removals is not { Count: > 0 } && _pendingWithdrawals.Count == 0)
        {
            return [];
        }

        var candidate = new List<KeyValuePair<string, ServerConfiguration>>(_serverConfigurations);
        var changed = _pendingWithdrawals.Count != 0;
        if (changed)
        {
            candidate.RemoveAll(file => _pendingWithdrawals.Contains(file.Key));
        }

        if (removals is not null)
        {
            foreach (var removal in removals)
            {
                if (candidate.RemoveAll(file => file.Key == removal.Path) != 0)
                {
                    _pendingWithdrawals.Add(removal.Path);
                    changed = true;
                }
            }
        }

        ApplyDetails[] outcomes = files is null ? [] : new ApplyDetails[files.Count];
        if (files is not null)
        {
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var path = file.Path.Path;
                _log.Debug<string, long, long>(
                    "Feature Flags: remote_config configuration file {Path} received (version {Version}, {Length} bytes)",
                    path,
                    file.Version,
                    file.Length);

                ServerConfiguration? configuration;
                try
                {
                    // Keep date-like strings as text, as the agentless parser does.
                    configuration = new NamedRawFile(file.Path, file.Contents).Deserialize<ServerConfiguration>(DateParseHandling.None).TypedFile;
                }
                catch (JsonException error)
                {
                    _log.Debug(error, "Feature Flags: could not parse remote_config configuration file {Path}", path);
                    outcomes[index] = ApplyDetails.FromError(path, "Could not parse Feature Flags configuration");
                    continue;
                }

                if (configuration is null)
                {
                    outcomes[index] = ApplyDetails.FromError(path, "Feature Flags configuration is empty");
                    continue;
                }

                // A replacement keeps its path. Leaving the old file in the merge keeps flags it dropped.
                candidate.RemoveAll(previous => previous.Key == path);
                candidate.Add(new KeyValuePair<string, ServerConfiguration>(path, configuration));
                outcomes[index] = new ApplyDetails(path);
                changed = true;
            }
        }

        if (!changed)
        {
            return outcomes;
        }

        var applied = _tryApplyConfigurations(candidate);
        if (applied)
        {
            _serverConfigurations = candidate;
            _pendingWithdrawals.Clear();
        }

        for (var index = 0; index < outcomes.Length; index++)
        {
            if (outcomes[index].ApplyState == ApplyStates.UNACKNOWLEDGED)
            {
                var path = outcomes[index].Filename;
                outcomes[index] = applied
                                      ? ApplyDetails.FromOk(path)
                                      : ApplyDetails.FromError(path, "Feature Flags evaluator did not install the configuration");
            }
        }

        return outcomes;
    }
}
