// <copyright file="DataStreamsHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal sealed class DataStreamsHttpHeaderHelper : HttpHeaderHelperBase
{
    public static readonly DataStreamsHttpHeaderHelper Instance = new();
    private readonly Lazy<string> _serializedHeaders;

    private DataStreamsHttpHeaderHelper()
    {
        _serializedHeaders = new(static () =>
        {
            var sb = StringBuilderCache.Acquire();
            foreach (var kvp in DataStreamsHttpHeaderNames.GetDefaultAgentHeaders())
            {
                sb.Append(kvp.Key);
                sb.Append(": ");
                sb.Append(kvp.Value);
                sb.Append(DatadogHttpValues.CrLf);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        });
    }

    public override KeyValuePair<string, string>[] DefaultHeaders => DataStreamsHttpHeaderNames.GetDefaultAgentHeaders();

    protected override string HttpSerializedDefaultHeaders => _serializedHeaders.Value;
}
