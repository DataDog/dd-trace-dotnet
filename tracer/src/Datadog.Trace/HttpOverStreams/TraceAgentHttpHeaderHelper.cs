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
        private readonly Lazy<string> _metadataHeaders = new(() =>
        {
            var sb = StringBuilderCache.Acquire();
            foreach (var kvp in AgentHttpHeaderNames.DefaultHeaders)
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

        public override KeyValuePair<string, string>[] DefaultHeaders => AgentHttpHeaderNames.DefaultHeaders;

        protected override string MetadataHeaders => _metadataHeaders.Value;
    }
}
