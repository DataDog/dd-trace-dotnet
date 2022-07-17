// <copyright file="BatchUploadApiFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Debugger.Sink
{
    internal static class BatchUploadApiFactory
    {
        public static IBatchUploadApi Create(ImmutableDebuggerSettings settings, IApiRequestFactory apiRequestFactory, IDiscoveryService discoveryService)
        {
            return AgentBatchUploadApi.Create(settings, apiRequestFactory, discoveryService);
        }
    }
}
