// <copyright file="DataStreamsHttpHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.DataStreamsMonitoring.Transport
{
    internal static class DataStreamsHttpHeaderNames
    {
        /// <summary>
        /// Gets the default constant headers that should be added to any request to the agent
        /// Similar to the defaults in <see cref="AgentHttpHeaderNames"/> (but not the same!)
        /// </summary>
        internal static KeyValuePair<string, string>[] GetDefaultAgentHeaders()
            => new KeyValuePair<string, string>[]
            {
                new(AgentHttpHeaderNames.Language, ".NET"),
                new(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion),
                new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
                new(AgentHttpHeaderNames.LanguageInterpreter, FrameworkDescription.Instance.Name),
                new(AgentHttpHeaderNames.LanguageVersion, FrameworkDescription.Instance.ProductVersion),
            };
    }
}
