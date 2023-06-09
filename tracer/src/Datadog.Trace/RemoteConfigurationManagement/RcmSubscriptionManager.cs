// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RcmSubscriptionManager>();

    private readonly List<ISubscription> _subscriptions = new();
    private readonly object _syncRoot = new();
    private BigInteger _capabilities;

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

    private void RefreshProductKeys() => ProductKeys = _subscriptions.SelectMany(s => s.ProductKeys).Distinct().ToList();
}
