// <copyright file="PerformedContextDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

/// <summary>
/// Duck-typed proxy for Hangfire.Server.PerformedContext, used for Datadog reverse method interception.
/// </summary>
public class PerformedContextDuck
{
    /// <summary>
    /// Gets or sets the background job that was executed.
    /// </summary>
    [DuckField(Name = "BackgroundJob")]
    public object BackgroundJob { get; set; }

    /// <summary>
    /// Gets or sets the Hangfire connection used for job storage.
    /// </summary>
    [DuckField(Name = "Connection")]
    public object Connection { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token associated with job execution.
    /// </summary>
    [DuckField(Name = "CancellationToken")]
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets or sets the dictionary of items shared across filters during job execution.
    /// </summary>
    [DuckField(Name = "Items")]
    public object Items { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the job execution was cancelled.
    /// </summary>
    [DuckField(Name = "Canceled")]
    public bool Canceled { get; set; }

    /// <summary>
    /// Gets or sets the exception that occurred during job execution, if any.
    /// </summary>
    [DuckField(Name = "Exception")]
    public Exception Exception { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception was handled by a filter.
    /// </summary>
    [DuckField(Name = "ExceptionHandled")]
    public bool ExceptionHandled { get; set; }

    /// <summary>
    /// Gets or sets the result of the job execution.
    /// </summary>
    [DuckField(Name = "Result")]
    public object Result { get; set; }
}
