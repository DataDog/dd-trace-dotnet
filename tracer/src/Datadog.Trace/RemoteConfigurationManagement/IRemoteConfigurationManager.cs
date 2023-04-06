// <copyright file="IRemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal interface IRemoteConfigurationManager
    {
        /// <summary>
        /// Start polling configurations asynchronously in an endless loop.
        /// </summary>
        Task StartPollingAsync();

        void SetCapability(BigInteger index, bool available);

        /// <summary>
        /// Subscribe to changes in rcm
        /// </summary>
        /// <param name="callback">callback func that returns the applied status. The callback takes first the changed configs and second, the removed configs, always by product name as a key</param>
        /// <param name="productKeys">productKeys (names)</param>
        /// <returns>the subscription</returns>
        ISubscription SubscribeToChanges(Func<Dictionary<string, List<RemoteConfiguration>>, Dictionary<string, List<RemoteConfigurationPath>>, List<ApplyDetails>> callback, params string[] productKeys);

        void Unsubscribe(ISubscription subscription);
    }
}
