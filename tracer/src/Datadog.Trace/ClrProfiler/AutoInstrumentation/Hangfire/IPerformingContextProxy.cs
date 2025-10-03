// <copyright file="IPerformingContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Server.PerformingContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Server/PerformingContext.cs
/// </remarks>
public interface IPerformingContextProxy : IPerformContextProxy
{
    /// <summary>
    /// Gets or sets a value indicating whether it gets or sets a value of System.Boolean
    /// </summary>
    bool Canceled { get; set; }
}
