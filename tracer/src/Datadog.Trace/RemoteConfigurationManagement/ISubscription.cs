// <copyright file="ISubscription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal interface ISubscription
    {
        public HashSet<string> ProductKeys { get; }

        Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>?, List<ApplyDetails>> Invoke { get; }

        void AddProductKeys(params string[] newProductKeys);

        void RemoveProductKeys(params string[] productKeys);
    }
}
