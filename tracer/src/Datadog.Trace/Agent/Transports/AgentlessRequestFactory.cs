// <copyright file="AgentlessRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequestFactory : IApiRequestFactory
{
    private readonly Uri _baseEndpoint;
    private readonly KeyValuePair<string, string>[] _defaultHeaders;

    static AgentlessRequestFactory()
    {
        NativeAgentless.Initialize();
        LifetimeManager.Instance.AddShutdownTask(_ =>
        {
            Tracer.Instance.FlushAsync().SafeWait();
            NativeAgentless.Shutdown();
        });
    }

    public AgentlessRequestFactory(Uri baseEndpoint, KeyValuePair<string, string>[] defaultHeaders)
    {
        _baseEndpoint = baseEndpoint;
        _defaultHeaders = defaultHeaders;
    }

    public string Info(Uri endpoint) => endpoint.ToString();

    public Uri GetEndpoint(string relativePath) => UriHelpers.Combine(_baseEndpoint, relativePath);

    public IApiRequest Create(Uri endpoint) => new AgentlessRequest(endpoint, _defaultHeaders);

    public void SetProxy(WebProxy proxy, NetworkCredential credential)
    {
    }
}
