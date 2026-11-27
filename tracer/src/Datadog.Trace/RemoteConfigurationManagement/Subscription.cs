// <copyright file="Subscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal delegate bool SnapshotInstaller(List<RemoteConfiguration> files, Dictionary<string, string> rejections, long targetGeneration, Func<Action, bool> tryCommit);

    internal sealed class Subscription : ISubscription
    {
        private readonly HashSet<string> _productKeys;

        public Subscription(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, ApplyDetails[]> callback, params string[] productKeys)
        {
            _productKeys = new HashSet<string>(productKeys);
            Invoke = (configsByProduct, removedConfigsByProduct) => Task.FromResult(callback(configsByProduct, removedConfigsByProduct));
        }

        public Subscription(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, Task<ApplyDetails[]>> callback, params string[] productKeys)
        {
            _productKeys = new HashSet<string>(productKeys);
            Invoke = callback;
        }

        public IReadOnlyCollection<string> ProductKeys => _productKeys;

        public Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, Task<ApplyDetails[]>> Invoke { get; }

        internal SnapshotInstaller? InstallSnapshot { get; init; }

        // Retained configuration may need a withdrawal after poll interest changes.
        internal IReadOnlyCollection<string>? RemovalProductKeys { get; init; }

#if SHARED_REMOTE_CONFIG
        internal object? DeliveryOwner { get; init; }
#endif

        internal bool HasSameProducts(string[] productKeys) => _productKeys.SetEquals(productKeys);
    }
}
