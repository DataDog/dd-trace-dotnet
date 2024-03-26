// <copyright file="DataStreamsHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Linq;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;

namespace Datadog.Trace.DataStreamsMonitoring.Transport;

internal class DataStreamsHttpHeaderHelper : HttpHeaderHelperBase
{
    private static string? _metadataHeaders = null;

    protected override string MetadataHeaders
    {
        get
        {
            if (_metadataHeaders is null)
            {
                var headers = DataStreamsHttpHeaderNames.GetDefaultAgentHeaders().Select(kvp => $"{kvp.Key}: {kvp.Value}{DatadogHttpValues.CrLf}");
                _metadataHeaders = string.Concat(headers);
            }

            return _metadataHeaders;
        }
    }

    protected override string ContentType => MimeTypes.MsgPack;
}
