// <copyright file="ISubscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal interface ISubscription : IDisposable
    {
        internal ReadOnlyCollection<string> ProductKeys { get; }

        public Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> Invoke { get; }

        public ISubscription AddProductKeys(params string[] newProductKeys);

        public ISubscription RemoveProductKeys(params string[] newProductKeys);
    }

    internal class Subscription : ISubscription
    {
        private readonly IRemoteConfigurationManager _remoteConfigurationManager;

        public Subscription(IRemoteConfigurationManager remoteConfigurationManager, Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> callback, params string[] productKeys)
        {
            _remoteConfigurationManager = remoteConfigurationManager;
            ProductKeys = productKeys.ToList().AsReadOnly();
            Invoke = callback;
        }

        public ReadOnlyCollection<string> ProductKeys { get; }

        public Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> Invoke { get; }

        public ISubscription AddProductKeys(params string[] newProductKeys)
        {
            Dispose();
            return _remoteConfigurationManager.SubscribeToChanges(Invoke, ProductKeys.Concat(newProductKeys).ToArray());
        }

        public ISubscription RemoveProductKeys(params string[] productKeys)
        {
            var newKeys = ProductKeys.Except(productKeys);
            return _remoteConfigurationManager.SubscribeToChanges(Invoke, newKeys.ToArray());
        }

        public void Dispose() => _remoteConfigurationManager.Unsubscribe(this);
    }
}
