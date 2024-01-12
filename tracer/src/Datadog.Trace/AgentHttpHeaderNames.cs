// <copyright file="AgentHttpHeaderNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace
{
    /// <summary>
    /// Names of HTTP headers that can be used when sending traces to the Trace Agent.
    /// </summary>
    internal static class AgentHttpHeaderNames
    {
        /// <summary>
        /// The language-specific tracer that generated this span.
        /// Always ".NET" for the .NET Tracer.
        /// </summary>
        public const string Language = "Datadog-Meta-Lang";

        /// <summary>
        /// The interpreter for the given language, e.g. ".NET Framework" or ".NET Core".
        /// </summary>
        public const string LanguageInterpreter = "Datadog-Meta-Lang-Interpreter";

        /// <summary>
        /// The interpreter version for the given language, e.g. "4.7.2" for .NET Framework or "2.1" for .NET Core.
        /// </summary>
        public const string LanguageVersion = "Datadog-Meta-Lang-Version";

        /// <summary>
        /// The version of the tracer that generated this span.
        /// </summary>
        public const string TracerVersion = "Datadog-Meta-Tracer-Version";

        /// <summary>
        /// The number of unique traces per request.
        /// </summary>
        public const string TraceCount = "X-Datadog-Trace-Count";

        /// <summary>
        /// The id of the container where the traced application is running.
        /// </summary>
        public const string ContainerId = "Datadog-Container-ID";

        /// <summary>
        /// The unique identifier of the container where the traced application is running, either as the container id
        /// or the cgroup node controller's inode.
        /// This differs from <see cref="ContainerId"/> which is always the container id, which may not always be
        /// accessible due to new Pod Security Standards starting in Kubernetes 1.25.
        /// </summary>
        public const string EntityId = "Datadog-Entity-ID";

        /// <summary>
        /// Tells the agent whether top-level spans are computed by the tracer
        /// </summary>
        public const string ComputedTopLevelSpan = "Datadog-Client-Computed-Top-Level";

        /// <summary>
        /// Version reported by the Datadog agent
        /// </summary>
        public const string AgentVersion = "Datadog-Agent-Version";

        /// <summary>
        /// Tells the agent whether stats are computer by the tracer
        /// </summary>
        public const string StatsComputation = "Datadog-Client-Computed-Stats";

        /// <summary>
        /// Tells the agent how many P0 traces were dropped as a result of stats computation in the tracer
        /// </summary>
        public const string DroppedP0Traces = "Datadog-Client-Dropped-P0-Traces";

        /// <summary>
        /// Tells the agent how many P0 spans were dropped as a result of stats computation in the tracer
        /// </summary>
        public const string DroppedP0Spans = "Datadog-Client-Dropped-P0-Spans";

        /// <summary>
        /// Gets the default constant header that should be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] DefaultHeaders { get; } =
        {
            new(Language, ".NET"),
            new(TracerVersion, TracerConstants.AssemblyVersion),
            new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
            new(LanguageInterpreter, FrameworkDescription.Instance.Name),
            new(LanguageVersion, FrameworkDescription.Instance.ProductVersion),
            new(ComputedTopLevelSpan, "1")
        };

        /// <summary>
        /// Gets the minimal constant header that can be added to any request to the agent
        /// </summary>
        internal static KeyValuePair<string, string>[] MinimalHeaders { get; } =
        {
            new(Language, ".NET"),
            new(TracerVersion, TracerConstants.AssemblyVersion),
            new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
        };
    }
}
