// <copyright file="IPerformContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Server.PerformContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Server/PerformContext.cs
/// </remarks>
public interface IPerformContextProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    IDictionary<string, object?> Items { get; }

    /// <summary>
    /// Gets a value of Hangfire.BackgroundJob
    /// </summary>
    IBackgroundJobProxy BackgroundJob { get; }

    /// <summary>
    /// Gets a value of System.String
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Gets a value of Hangfire.Common.Job
    /// </summary>
    object Job { get; }

    /// <summary>
    /// Gets a value of System.DateTime
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Calls method: T Hangfire.Server.PerformContext::GetJobParameter[T](System.String)
    /// </summary>
    T GetJobParameter<T>(string name);
}
