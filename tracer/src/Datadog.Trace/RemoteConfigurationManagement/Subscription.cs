// <copyright file="Subscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class Subscription : ISubscription
    {
        public Subscription(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> callback, params string[] productKeys)
        {
            ProductKeys = new HashSet<string>(productKeys);
            Invoke = callback;
        }

        public HashSet<string> ProductKeys { get; }

        public Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> Invoke { get; }

        public void AddProductKeys(params string[] newProductKeys)
        {
            ProductKeys.UnionWith(newProductKeys);
        }

        public void RemoveProductKeys(params string[] productKeys)
        {
            ProductKeys.ExceptWith(productKeys);
        }
    }
}
