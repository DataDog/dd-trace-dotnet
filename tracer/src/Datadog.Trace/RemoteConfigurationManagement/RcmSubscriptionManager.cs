// <copyright file="RcmSubscriptionManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class RcmSubscriptionManager : IRcmSubscriptionManager
{
    public static readonly IRcmSubscriptionManager Instance = new RcmSubscriptionManager();
    private readonly List<ISubscription> _subscriptions = new();
    private readonly object _syncRoot = new();

    public bool HasAnySubscription => _subscriptions.Count > 0;

    public ICollection<string> ProductKeys
    {
        get
        {
            lock (_syncRoot)
            {
                return _subscriptions.SelectMany(s => s.ProductKeys).ToList();
            }
        }
    }

    public void SubscribeToChanges(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.Contains(subscription))
            {
                _subscriptions.Add(subscription);
            }
        }
    }

    public void Unsubscribe(ISubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(subscription);
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
            var configByProduct =
                configByProducts.Where(c => subscription.ProductKeys.Contains(c.Key))
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
}
