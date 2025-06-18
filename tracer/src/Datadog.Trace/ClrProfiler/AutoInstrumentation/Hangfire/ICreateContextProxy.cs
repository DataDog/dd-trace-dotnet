// <copyright file="ICreateContextProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// DuckTyping interface for Hangfire.Client.CreateContext
/// </summary>
/// <remarks>
/// https://github.com/HangfireIO/Hangfire/blob/96c5d825ab3ee6f123f9e041ac301881e168e508/src/Hangfire.Core/Client/CreateContext.cs
/// </remarks>
internal interface ICreateContextProxy : IDuckType
{
    /// <summary>
    /// Gets a value of Hangfire.JobStorage
    /// </summary>
    object Storage { get; }

    /// <summary>
    /// Gets a value of Hangfire.Storage.IStorageConnection
    /// </summary>
    object Connection { get; }

    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    object Items { get; }

    /// <summary>
    /// Gets a value of System.Collections.Generic.IDictionary`2[System.String,System.Object]
    /// </summary>
    object Parameters { get; }

    /// <summary>
    /// Gets a value of Hangfire.Common.Job
    /// </summary>
    object Job { get; }

    /// <summary>
    /// Gets a value of Hangfire.States.IState
    /// </summary>
    object InitialState { get; }

    /// <summary>
    /// Gets a value of Hangfire.Profiling.IProfiler
    /// </summary>
    object Profiler { get; }

    /// <summary>
    /// Gets or sets a value of Hangfire.Client.IBackgroundJobFactory
    /// </summary>
    object Factory { get; set; }
}
