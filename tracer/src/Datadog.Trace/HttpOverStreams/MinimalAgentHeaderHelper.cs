// <copyright file="MinimalAgentHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Linq;

namespace Datadog.Trace.HttpOverStreams;

internal class MinimalAgentHeaderHelper : HttpHeaderHelperBase
{
    private static string? _metadataHeaders = null;
    private static string? _metadataHeadersWithContainerId = null;
    private readonly string? _containerId;

    public MinimalAgentHeaderHelper(string? containerId = null)
    {
        _containerId = containerId;
    }

    protected override string MetadataHeaders
    {
        get
        {
            if (_metadataHeaders == null)
            {
                var headers = AgentHttpHeaderNames.MinimalHeaders.Select(kvp => $"{kvp.Key}: {kvp.Value}{DatadogHttpValues.CrLf}");
                _metadataHeaders = string.Concat(headers);
            }

            if (_containerId != null)
            {
                if (_metadataHeadersWithContainerId == null)
                {
                    // Assuming we'd always get the same container ID. The first time, we use its value to build the header,
                    // then it's just a marker that we want the header with the containerID in it, and we don't look at its value.
                    _metadataHeadersWithContainerId = _metadataHeaders + $"{AgentHttpHeaderNames.ContainerId}: {_containerId}{DatadogHttpValues.CrLf}";
                }

                return _metadataHeadersWithContainerId;
            }

            return _metadataHeaders;
        }
    }

    protected override string ContentType => "application/json";
}
