// <copyright file="RemoteConfigurationManager.Subscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal partial class RemoteConfigurationManager
    {
        private readonly List<ISubscription> _subscriptions = new();
        private HashSet<string> _subscriptionsProductKeys = new();

        /// <summary>
        /// Subscribe to changes in rcm
        /// </summary>
        /// <param name="callback">callback func that returns the applied status. The callback takes first the changed configs and second, the removed configs, always by product name as a key</param>
        /// <param name="productKeys">productKeys (names)</param>
        /// <returns>the subscription</returns>
        public ISubscription SubscribeToChanges(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> callback, params string[] productKeys)
        {
            var subscription = new Subscription(this, callback, productKeys);
            _subscriptions.Add(subscription);
            _subscriptionsProductKeys.UnionWith(productKeys);
            return subscription;
        }

        public void Unsubscribe(ISubscription subscription)
        {
            _subscriptions.Remove(subscription);
            // we cant just remove the keys here as we want to allow more than one subscriptions to the same key
            _subscriptionsProductKeys = new HashSet<string>(_subscriptions.SelectMany(s => s.ProductKeys));
        }
    }
}
