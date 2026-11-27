// <copyright file="RemoteConfigurationApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Agent;
#if SHARED_REMOTE_CONFIG
using Datadog.Trace.Agent.Transports;
#endif
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal static class RemoteConfigurationApiFactory
    {
        private const string ProductName = "rcm";
        private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(15);

        public static IRemoteConfigurationApi Create(ExporterSettings exporterSettings)
        {
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: ProductName,
                tcpTimeout: AgentTimeout,
                httpHeaderHelper: MinimalAgentHeaderHelper.Instance);

            return new RemoteConfigurationApi<IApiRequest>(apiRequestFactory);
        }

#if SHARED_REMOTE_CONFIG
        internal static RemoteConfigurationApi<HttpClientRequest> CreateStreaming(ExporterSettings exporterSettings)
        {
            var factory = AgentTransportStrategy.GetHttpClient(
                exporterSettings,
                productName: ProductName,
                tcpTimeout: AgentTimeout,
                httpHeaderHelper: MinimalAgentHeaderHelper.Instance);
            return new RemoteConfigurationApi<HttpClientRequest>(factory);
        }
#endif
    }
}
