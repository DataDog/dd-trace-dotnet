// <copyright file="EventPlatformHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Agent;

namespace Datadog.Trace.HttpOverStreams;

internal sealed class EventPlatformHeaderHelper : HttpHeaderHelperBase
{
    public static readonly EventPlatformHeaderHelper Instance = new();

    private EventPlatformHeaderHelper()
    {
        DefaultHeaders =
        [
            ..AgentHttpHeaderNames.MinimalHeaders,
            new(
                EventPlatformProxyConstants.SubdomainHeaderName,
                EventPlatformProxyConstants.EventPlatformIntakeSubdomain),
        ];
        HttpSerializedDefaultHeaders =
            AgentHttpHeaderNames.HttpSerializedMinimalHeaders
            + EventPlatformProxyConstants.SubdomainHeaderName
            + ": "
            + EventPlatformProxyConstants.EventPlatformIntakeSubdomain
            + DatadogHttpValues.CrLf;
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string HttpSerializedDefaultHeaders { get; }
}
