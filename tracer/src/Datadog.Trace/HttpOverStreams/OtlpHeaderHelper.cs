// <copyright file="OtlpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace;
using Datadog.Trace.Util;

namespace Datadog.Trace.HttpOverStreams;

internal sealed class OtlpHeaderHelper : HttpHeaderHelperBase
{
    private readonly string _serializedHeaders;

    public OtlpHeaderHelper(KeyValuePair<string, string>[] signalHeaders)
    {
        DefaultHeaders =
        [
            ..signalHeaders,
            new(HttpHeaderNames.TracingEnabled, "false"),
        ];

        var sb = StringBuilderCache.Acquire();
        foreach (var kvp in DefaultHeaders)
        {
            sb.Append(kvp.Key);
            sb.Append(": ");
            sb.Append(kvp.Value);
            sb.Append(DatadogHttpValues.CrLf);
        }

        _serializedHeaders = StringBuilderCache.GetStringAndRelease(sb);
    }

    public override KeyValuePair<string, string>[] DefaultHeaders { get; }

    protected override string HttpSerializedDefaultHeaders => _serializedHeaders;
}
