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
        internal const int AgentTimeoutMs = 15_000;
        private const string ProductName = "rcm";
        private static readonly TimeSpan AgentTimeout = TimeSpan.FromMilliseconds(AgentTimeoutMs);

        public static IRemoteConfigurationApi Create(ExporterSettings exporterSettings)
        {
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: ProductName,
                tcpTimeout: AgentTimeout,
                httpHeaderHelper: MinimalAgentHeaderHelper.Instance);

            return new RemoteConfigurationApi(apiRequestFactory);
        }

#if SHARED_REMOTE_CONFIG
        internal static IRemoteConfigurationApi CreateStreaming(ExporterSettings exporterSettings)
        {
            IStreamingApiRequestFactory factory = AgentTransportStrategy.GetHttpClient(
                exporterSettings,
                productName: ProductName,
                tcpTimeout: AgentTimeout,
                httpHeaderHelper: MinimalAgentHeaderHelper.Instance);
            return new RemoteConfigurationApi(factory);
        }
#endif
    }
}
