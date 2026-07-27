// <copyright file="ServerlessCompatPipeNames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Serverless
{
    /// <summary>
    /// Unique pipe names generated at tracer startup for coordinating with the Serverless
    /// Compatibility Layer. A single instance is created by <see cref="Datadog.Trace.Configuration.TracerSettings.SettingsManager"/>
    /// and reused across every <see cref="Datadog.Trace.Configuration.ExporterSettings"/> construction
    /// (including reconfigs triggered by manual or remote configuration changes) so the compat
    /// layer sees a stable pipe name for the lifetime of the process. <c>null</c> values indicate
    /// the tracer should not attempt pipe transport (non-Windows, non-Azure-Functions, using the
    /// AAS Site Extension, or the compat layer is missing or too old).
    /// </summary>
    internal sealed record ServerlessCompatPipeNames(string? TracesPipeName, string? MetricsPipeName)
    {
        internal static ServerlessCompatPipeNames None { get; } = new(TracesPipeName: null, MetricsPipeName: null);
    }
}
