// <copyright file="DataStreamsHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal sealed class DataStreamsHttpHeaderHelper : HttpHeaderHelperBase
{
    private readonly Lazy<string> _metadataHeaders = new(() =>
    {
        var sb = StringBuilderCache.Acquire();
        foreach (var kvp in DataStreamsHttpHeaderNames.GetDefaultAgentHeaders())
        {
            sb.Append(kvp.Key);
            sb.Append(": ");
            sb.Append(kvp.Value);
            sb.Append(DatadogHttpValues.CrLf);
        }

        // remove last char
        sb.Remove(sb.Length - 1, 1);

        return StringBuilderCache.GetStringAndRelease(sb);
    });

    protected override string MetadataHeaders => _metadataHeaders.Value;

    protected override string ContentType => MimeTypes.MsgPack;
}
