// <copyright file="Subscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class Subscription
    {
        private readonly Action _onChange;

        public Subscription(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> callback, Action onChange, params string[] productKeys)
        {
            _onChange = onChange;
            ProductKeys = productKeys.ToList();
            Callback = callback;
        }

        internal List<string> ProductKeys { get; set; }

        internal Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> Callback { get; set; }

        internal List<RemoteConfiguration>? NextConfigs { get; set; }

        public void UnsubscribeProducts(params string[] names)
        {
            foreach (var name in names)
            {
                ProductKeys.Remove(name);
            }
        }

        public void SubscribeProducts(params string[] names)
        {
            ProductKeys.AddRange(names);
            _onChange();
        }
    }
}
