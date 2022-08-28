// <copyright file="RemoteConfigurationApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal class RemoteConfigurationApiFactory
    {
        public static IRemoteConfigurationApi Create(RemoteConfigurationSettings remoteConfigurationSettings, IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return
                string.IsNullOrWhiteSpace(remoteConfigurationSettings.FilePath)
                    ? RemoteConfigurationApi.Create(apiRequestFactory, discoveryService)
                    : RemoteConfigurationFileApi.Create(remoteConfigurationSettings);
        }
    }
}
