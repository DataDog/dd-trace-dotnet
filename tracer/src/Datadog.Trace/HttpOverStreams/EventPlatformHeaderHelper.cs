// <copyright file="EventPlatformHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.HttpOverStreams;

internal sealed class EventPlatformHeaderHelper : HttpHeaderHelperBase
{
    public static readonly EventPlatformHeaderHelper Instance = new();

    private EventPlatformHeaderHelper()
    {
        const string evpHeaderKey = "X-Datadog-EVP-Subdomain";
        const string evpHeaderValue = "event-platform-intake";
        DefaultHeaders =
        [
            ..AgentHttpHeaderNames.MinimalHeaders,
            new(evpHeaderKey, evpHeaderValue),
        ];
        HttpSerializedDefaultHeaders = $"{AgentHttpHeaderNames.HttpSerializedMinimalHeaders}{evpHeaderKey}: {evpHeaderValue}{DatadogHttpValues.CrLf}";
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string HttpSerializedDefaultHeaders { get; }
}
