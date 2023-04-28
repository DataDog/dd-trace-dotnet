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
        private readonly HashSet<string> _productKeys;

        public Subscription(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, IEnumerable<ApplyDetails>> callback, params string[] productKeys)
        {
            _productKeys = new HashSet<string>(productKeys);
            Invoke = callback;
        }

        public IReadOnlyCollection<string> ProductKeys => _productKeys;

        public Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, IEnumerable<ApplyDetails>> Invoke { get; }
    }
}
