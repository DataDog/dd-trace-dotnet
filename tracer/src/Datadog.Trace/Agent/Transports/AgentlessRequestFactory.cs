// <copyright file="AgentlessRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP
using System;
using System.Net;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequestFactory : IApiRequestFactory
{
    public string Info(Uri endpoint) => endpoint.ToString();

    public Uri GetEndpoint(string relativePath) => new(relativePath, UriKind.Relative);

    public IApiRequest Create(Uri endpoint) => new AgentlessRequest(endpoint);

    public void SetProxy(WebProxy proxy, NetworkCredential credential)
    {
    }
}
#endif
