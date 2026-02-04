// <copyright file="TraceAgentHttpHeaderHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.HttpOverStreams
{
    internal sealed class TraceAgentHttpHeaderHelper : HttpHeaderHelperBase
    {
        public static readonly TraceAgentHttpHeaderHelper Instance = new();
        private readonly Lazy<string> _metadataHeaders;

        private TraceAgentHttpHeaderHelper()
        {
            _metadataHeaders = new(static () =>
            {
                var sb = StringBuilderCache.Acquire();
                foreach (var kvp in AgentHttpHeaderNames.DefaultHeaders)
                {
                    sb.Append(kvp.Key);
                    sb.Append(": ");
                    sb.Append(kvp.Value);
                    sb.Append(DatadogHttpValues.CrLf);
                }

                return StringBuilderCache.GetStringAndRelease(sb);
            });
        }

        public override KeyValuePair<string, string>[] DefaultHeaders => AgentHttpHeaderNames.DefaultHeaders;

        protected override string MetadataHeaders => _metadataHeaders.Value;
    }
}
