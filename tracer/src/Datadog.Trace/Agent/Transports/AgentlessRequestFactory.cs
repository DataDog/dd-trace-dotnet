// <copyright file="AgentlessRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Net;
using System.Threading;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequestFactory : IApiRequestFactory
{
    static AgentlessRequestFactory()
    {
        NativeAgentless.Initialize();
        LifetimeManager.Instance.AddShutdownTask(_ =>
        {
            Tracer.Instance.FlushAsync().SafeWait();
            NativeAgentless.Shutdown();
        });
    }

    public string Info(Uri endpoint) => endpoint.OriginalString;

    public Uri GetEndpoint(string relativePath) => new(relativePath, UriKind.Relative);

    public IApiRequest Create(Uri endpoint) => new AgentlessRequest(endpoint);

    public void SetProxy(WebProxy proxy, NetworkCredential credential)
    {
    }
}
