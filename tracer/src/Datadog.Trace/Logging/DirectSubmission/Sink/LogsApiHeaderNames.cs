// <copyright file="LogsApiHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

#nullable enable

namespace Datadog.Trace.Logging.DirectSubmission.Sink
{
    internal static class LogsApiHeaderNames
    {
        /// <summary>
        /// Gets the default constant header that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] DefaultHeaders { get; } =
        {
            new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
        };
    }
}
