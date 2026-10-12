// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();

    private readonly object _syncRoot = new();

    // Ideally this would be an ImmutableArray but that's not available in net461
    private IReadOnlyList<ISubscription> _subscriptions = [];

    private BigInteger _capabilities;

    // What was last sent to the client
    private IRemoteConfigClient? _pushedTo;
    private ICollection<string>? _pushedProductKeys;
    private BigInteger _pushedCapabilities;

    public string ClientId { get; } = Guid.NewGuid().ToString();

    public bool HasAnySubscription => _subscriptions.Count > 0;

    // this list shouldn't be recalculated everytime we access it as it is used by RemoteConfigurationManager to build an rcm request every x seconds
    public ICollection<string> ProductKeys { get; private set; } = [];

    public void SubscribeToChanges(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.Contains(subscription))
            {
                _subscriptions = [.. _subscriptions, subscription];
            }

            RefreshProductKeys();
        }
    }

    public void Replace(ISubscription oldSubscription, ISubscription newSubscription)
    {
        lock (_syncRoot)
        {
            var newSubscriptions = new List<ISubscription>(_subscriptions.Count);

            bool found = false;

            foreach (var subscription in _subscriptions)
            {
                var subscriptionToAdd = subscription == oldSubscription ? newSubscription : subscription;

                if (subscriptionToAdd == newSubscription)
                {
                    found = true;
                }

                newSubscriptions.Add(subscriptionToAdd);
            }

            if (!found)
            {
                newSubscriptions.Add(newSubscription);
            }

            _subscriptions = newSubscriptions;

            RefreshProductKeys();
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions = new List<ISubscription>(_subscriptions.Where(s => s != subscription));
            RefreshProductKeys();
        }
    }

    /// <summary>
    /// Called by RCM
    /// </summary>
    private async Task<List<ApplyDetails>?> Update(Dictionary<string, List<RemoteConfiguration>>? configByProducts, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct)
    {
        List<ApplyDetails>? results = null;

        var subscriptions = Volatile.Read(ref _subscriptions);

        foreach (var subscription in subscriptions)
        {
            Dictionary<string, List<RemoteConfiguration>>? filteredConfigs = null;
            if (configByProducts is not null)
            {
                foreach (var productKey in subscription.ProductKeys)
                {
                    if (configByProducts.TryGetValue(productKey, out var configs))
                    {
                        filteredConfigs ??= new(subscription.ProductKeys.Count);
                        filteredConfigs[productKey] = configs;
                    }
                }
            }

            var haveChanges = filteredConfigs?.Count > 0 || removedConfigsByProduct?.Count > 0;
            if (!haveChanges)
            {
                continue;
            }

            try
            {
                results ??= new();
                results.AddRange(await subscription.Invoke(filteredConfigs ?? [], removedConfigsByProduct).ConfigureAwait(false));
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
        lock (_syncRoot)
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
    }

    public void Poll(IRemoteConfigClient client)
    {
        PushProductsIfChanged(client);

        RemoteConfigurationChanges changes;
        try
        {
            changes = client.Fetch();
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error fetching remote configurations");
            return;
        }

        if (changes.IsEmpty)
        {
            return;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Received remote configurations. Updated: {Updated}. Removed: {Removed}.",
                string.Join(",", changes.Updated.Select(c => c.Path.Path)),
                string.Join(",", changes.Removed.Select(p => p.Path)));
        }

        Dictionary<string, List<RemoteConfiguration>>? configByProducts = null;
        foreach (var config in changes.Updated)
        {
            configByProducts ??= [];
            if (!configByProducts.TryGetValue(config.Path.Product, out var configs))
            {
                configByProducts[config.Path.Product] = configs = [];
            }

            configs.Add(config);
        }

        Dictionary<string, List<RemoteConfigurationPath>>? removedConfigsByProduct = null;
        foreach (var path in changes.Removed)
        {
            removedConfigsByProduct ??= [];
            if (!removedConfigsByProduct.TryGetValue(path.Product, out var paths))
            {
                removedConfigsByProduct[path.Product] = paths = [];
            }

            paths.Add(path);
        }

        // Polling happens on a dedicated thread, so blocking on the (rarely asynchronous) subscribers is fine
        var results = Update(configByProducts, removedConfigsByProduct).GetAwaiter().GetResult();
        if (results is null)
        {
            return;
        }

        foreach (var result in results)
        {
            if (result.ApplyState == ApplyStates.UNACKNOWLEDGED)
            {
                continue;
            }

            client.SetApplyState(result.Filename, result.ApplyState, result.Error);
        }
    }

    private void PushProductsIfChanged(IRemoteConfigClient client)
    {
        ICollection<string> productKeys;
        BigInteger capabilities;
        lock (_syncRoot)
        {
            productKeys = ProductKeys;
            capabilities = _capabilities;
        }

        if (ReferenceEquals(client, _pushedTo) && ReferenceEquals(productKeys, _pushedProductKeys) && capabilities == _pushedCapabilities)
        {
            return;
        }

        client.SetProducts(productKeys, capabilities);
        _pushedTo = client;
        _pushedProductKeys = productKeys;
        _pushedCapabilities = capabilities;
    }

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
