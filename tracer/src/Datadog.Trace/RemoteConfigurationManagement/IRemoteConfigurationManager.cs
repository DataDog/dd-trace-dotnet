// <copyright file="IRemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
    }
}
