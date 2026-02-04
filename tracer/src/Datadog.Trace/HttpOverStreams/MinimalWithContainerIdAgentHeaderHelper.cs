// <copyright file="MinimalWithContainerIdAgentHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.HttpOverStreams;

internal sealed class MinimalWithContainerIdAgentHeaderHelper : HttpHeaderHelperBase
{
    private readonly Lazy<string> _metadataHeaders;

    public MinimalWithContainerIdAgentHeaderHelper(string containerId)
    {
        DefaultHeaders =
        [
            ..AgentHttpHeaderNames.MinimalHeaders,
            new(AgentHttpHeaderNames.ContainerId, containerId),
        ];
        _metadataHeaders = new(() => $"{AgentHttpHeaderNames.HttpSerializedMinimalHeaders}{AgentHttpHeaderNames.ContainerId}: {containerId}{DatadogHttpValues.CrLf}");
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string MetadataHeaders => _metadataHeaders.Value;

    protected override string ContentType => "application/json";
}
