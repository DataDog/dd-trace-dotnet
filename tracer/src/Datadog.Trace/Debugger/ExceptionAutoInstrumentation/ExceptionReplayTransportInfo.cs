// <copyright file="ExceptionReplayTransportInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal readonly struct ExceptionReplayTransportInfo
    {
        public ExceptionReplayTransportInfo(IApiRequestFactory apiRequestFactory, IDiscoveryService? discoveryService, string? staticEndpoint, bool isAgentless)
        {
            ApiRequestFactory = apiRequestFactory;
            DiscoveryService = discoveryService;
            StaticEndpoint = staticEndpoint;
            IsAgentless = isAgentless;
        }

        public IApiRequestFactory ApiRequestFactory { get; }

        public IDiscoveryService? DiscoveryService { get; }

        public string? StaticEndpoint { get; }

        public bool IsAgentless { get; }
    }
}
