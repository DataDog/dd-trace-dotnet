// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class RcmSubscriptionManager : IRcmSubscriptionManager
{
    private const int RootVersion = 1;

    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();

    private readonly List<ISubscription> _subscriptions = new();
    private readonly object _syncRoot = new();

    /// <summary>
    /// Key is the path
    /// </summary>
    private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();

    private readonly string _id;

    private string? _backendClientState;
    private int _targetsVersion;
    private BigInteger _capabilities;

    public RcmSubscriptionManager()
    {
        _id = Guid.NewGuid().ToString();
    }

    public bool HasAnySubscription => _subscriptions.Count > 0;

    // this list shouldn't be recalculated everytime we access it as it is used by RemoteConfigurationManager to build an rcm request every x seconds
    public ICollection<string> ProductKeys { get; private set; } = new List<string>();

    public void SubscribeToChanges(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.Contains(subscription))
            {
                _subscriptions.Add(subscription);
            }

            RefreshProductKeys();
        }
    }

    public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(oldSubscription);

            if (!_subscriptions.Contains(newSubscription))
            {
                _subscriptions.Add(newSubscription);
            }

            RefreshProductKeys();
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(subscription);
            RefreshProductKeys();
        }
    }

    /// <summary>
    /// Called by RCM
    /// </summary>
    public List<ApplyDetails> Update(Dictionary<string, List<RemoteConfiguration>> configByProducts, Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct)
    {
        List<ApplyDetails> results = new();
        List<ISubscription> subscriptions;

        lock (_syncRoot)
        {
            subscriptions = _subscriptions.ToList();
        }

        foreach (var subscription in subscriptions)
        {
            var configByProduct = configByProducts.Where(c => subscription.ProductKeys.Contains(c.Key))
                                                  .ToDictionary(c => c.Key, c => c.Value);

            if (configByProduct.Count == 0 && removedConfigsByProduct?.Count == 0)
            {
                continue;
            }

            try
            {
                results.AddRange(subscription.Invoke(configByProduct, removedConfigsByProduct));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to apply remote configurations for products {ProductKeys}", string.Join(", ", subscription.ProductKeys));
            }
        }

        return results;
    }

    public void SetCapability(BigInteger index, bool available)
    {
        if (available)
        {
            _capabilities |= index;
        }
        else
        {
            _capabilities &= ~index;
        }
    }

    public byte[] GetCapabilities()
    {
        // capabilitiesArray needs to be big endian
#if NETCOREAPP
        var capabilitiesArray = _capabilities.ToByteArray(true, true);
#else
        var capabilitiesArray = _capabilities.ToByteArray();
        Array.Reverse(capabilitiesArray);
#endif

        return capabilitiesArray;
    }

    public GetRcmRequest BuildRequest(RcmClientTracer rcmTracer, string? lastPollError)
    {
        lock (_syncRoot)
        {
            var cachedTargetFiles = new List<RcmCachedTargetFile>();
            var configStates = new List<RcmConfigState>();
            var appliedConfigs = _appliedConfigurations.Values;

            foreach (var cache in appliedConfigs)
            {
                cachedTargetFiles.Add(new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList()));
                configStates.Add(new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product, cache.ApplyState, cache.Error));
            }

            var rcmState = new RcmClientState(RootVersion, _targetsVersion, configStates, lastPollError != null, lastPollError, _backendClientState);
            var rcmClient = new RcmClient(_id, ProductKeys, rcmTracer, rcmState, GetCapabilities());
            var rcmRequest = new GetRcmRequest(rcmClient, cachedTargetFiles);

            return rcmRequest;
        }
    }

    public void ProcessResponse(GetRcmResponse response)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            var description = response.TargetFiles.Count > 0
                                  ? "with the following paths: " + string.Join(",", response.TargetFiles.Select(t => t.Path))
                                  : "that is empty.";
            Log.Debug("Received Remote Configuration response {ResponseDescription}.", description);
        }

        lock (_syncRoot)
        {
            var configByProducts = new Dictionary<string, List<RemoteConfiguration>>();
            var receivedPaths = new List<string>();

            // handle new configurations
            foreach (var clientConfig in response.ClientConfigs)
            {
                var remoteConfigurationPath = RemoteConfigurationPath.FromPath(clientConfig);
                receivedPaths.Add(remoteConfigurationPath.Path);
                var signed = response.Targets.Signed.Targets;
                var targetFiles =
                    (response.TargetFiles ?? Enumerable.Empty<RcmFile>()).ToDictionary(f => f.Path, f => f);

                if (!signed.TryGetValue(remoteConfigurationPath.Path, out var signedTarget))
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in targets");
                }

                if (!ProductKeys.Contains(remoteConfigurationPath.Product))
                {
                    Log.Warning("Received config {RemoteConfigurationPath} for a product that was not requested", remoteConfigurationPath);
                    continue;
                }

                var isConfigApplied =
                    _appliedConfigurations.TryGetValue(remoteConfigurationPath.Path, out var appliedConfig) &&
                    appliedConfig.Hashes.SequenceEqual(signedTarget.Hashes);
                if (isConfigApplied)
                {
                    continue;
                }

                if (!targetFiles.TryGetValue(remoteConfigurationPath.Path, out var targetFile))
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in target files");
                }

                var remoteConfigurationCache = new RemoteConfigurationCache(remoteConfigurationPath, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
                _appliedConfigurations[remoteConfigurationCache.Path.Path] = remoteConfigurationCache;

                var remoteConfiguration = new RemoteConfiguration(remoteConfigurationPath, targetFile.Raw, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
                if (!configByProducts.ContainsKey(remoteConfigurationPath.Product))
                {
                    configByProducts[remoteConfigurationPath.Product] = new List<RemoteConfiguration>();
                }

                configByProducts[remoteConfigurationPath.Product].Add(remoteConfiguration);
            }

            Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct = new();
            // handle removed configurations
            foreach (var appliedConfiguration in _appliedConfigurations)
            {
                if (receivedPaths.Contains(appliedConfiguration.Key))
                {
                    continue;
                }

                if (!removedConfigsByProduct.ContainsKey(appliedConfiguration.Value.Path.Product))
                {
                    removedConfigsByProduct[appliedConfiguration.Value.Path.Product] =
                        new List<RemoteConfigurationPath>();
                }

                removedConfigsByProduct[appliedConfiguration.Value.Path.Product].Add(appliedConfiguration.Value.Path);
            }

            // update applied configurations after removal
            foreach (var removedConfig in removedConfigsByProduct.Values)
            {
                foreach (var value in removedConfig)
                {
                    _appliedConfigurations.Remove(value.Path);
                }
            }

            var results = Update(configByProducts, removedConfigsByProduct);

            foreach (var result in results)
            {
                switch (result.ApplyState)
                {
                    case ApplyStates.UNACKNOWLEDGED:
                        // Do nothing
                        break;
                    case ApplyStates.ACKNOWLEDGED:
                        _appliedConfigurations[result.Filename].Applied();
                        break;
                    case ApplyStates.ERROR:
                        _appliedConfigurations[result.Filename].ErrorOccured(result.Error);
                        break;
                    default:
                        Log.Warning("Unexpected ApplyState: {ApplyState}", result.ApplyState);
                        break;
                }
            }

            _targetsVersion = response.Targets.Signed.Version;
            _backendClientState = response.Targets.Signed.Custom?.OpaqueBackendState;
        }
    }

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
