// <copyright file="IJobDiagnosticDataProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

/// <summary>
/// DuckTyping interface for Quartz.Logging.JobDiagnosticData
/// </summary>

/// <summary>
/// DuckTyping interface for Quartz.Logging.JobDiagnosticData
/// </summary>
/// <summary>
/// DuckTyping interface for Quartz.Logging.JobDiagnosticData
/// </summary>
internal interface IJobDiagnosticDataProxy : IDuckType
{
    DuckTypeITriggerProxy Trigger { get; }

    bool Recovering { get; }

    object RecoveringTriggerKey { get; }

    int RefireCount { get; }

    object MergedJobDataMap { get; }

    DuckTypeIJobDetailProxy JobDetail { get; }

    DateTimeOffset FireTimeUtc { get; }

    DateTimeOffset? ScheduledFireTimeUtc { get; }

    DateTimeOffset? PreviousFireTimeUtc { get; }

    DateTimeOffset? NextFireTimeUtc { get; }

    string FireInstanceId { get; }

    object Result { get; }

    TimeSpan JobRunTime { get; }

    string SchedulerId { get; }

    string SchedulerName { get; }
}

/// <summary>
/// DuckTyping struct for Quartz.ITrigger
/// </summary>
[DuckCopy]
internal struct DuckTypeITriggerProxy
{
    // /// <summary>
    // /// Gets a value of Quartz.TriggerKey
    // /// </summary>
    // private object key;
    //
    // /// <summary>
    // /// Gets a value of Quartz.JobKey
    // /// </summary>
    // private object jobKey;
    //
    // /// <summary>
    // /// Gets a value of System.String
    // /// </summary>
    // private string description;
    //
    // /// <summary>
    // /// Gets a value of System.String
    // /// </summary>
    // private string calendarName;
    //
    // /// <summary>
    // /// Gets a value of Quartz.JobDataMap
    // /// </summary>
    // private object jobDataMap;
    //
    // /// <summary>
    // /// Gets a value of System.Nullable`1[System.DateTimeOffset]
    // /// </summary>
    // private DateTimeOffset? finalFireTimeUtc;
    //
    // /// <summary>
    // /// Gets a value of System.Int32
    // /// </summary>
    // private int misfireInstruction;
    //
    // /// <summary>
    // /// Gets a value of System.Nullable`1[System.DateTimeOffset]
    // /// </summary>
    // private DateTimeOffset? endTimeUtc;
    //
    // /// <summary>
    // /// Gets a value of System.DateTimeOffset
    // /// </summary>
    // private DateTimeOffset startTimeUtc;
    //
    // /// <summary>
    // /// Gets a value of System.Int32
    // /// </summary>
    // private int priority;
    //
    // /// <summary>
    // /// Gets a value of System.Boolean
    // /// </summary>
    // private bool hasMillisecondPrecision;
}

/// <summary>
/// DuckTyping struct for Quartz.IJobDetail
/// </summary>
[DuckCopy]
internal struct DuckTypeIJobDetailProxy
{
    /// <summary>
    /// Gets a value of Quartz.JobKey
    /// </summary>
    internal object Key;

    // /// <summary>
    // /// Gets a value of System.String
    // /// </summary>
    // private string description;
    //
    // /// <summary>
    // /// Gets a value of System.Type
    // /// </summary>
    // private Type jobType;
    //
    // /// <summary>
    // /// Gets a value of Quartz.JobDataMap
    // /// </summary>
    // private object jobDataMap;
    //
    // /// <summary>
    // /// Gets a value of System.Boolean
    // /// </summary>
    // private bool durable;
    //
    // /// <summary>
    // /// Gets a value of System.Boolean
    // /// </summary>
    // private bool persistJobDataAfterExecution;
    //
    // /// <summary>
    // /// Gets a value of System.Boolean
    // /// </summary>
    // private bool concurrentExecutionDisallowed;
    //
    // /// <summary>
    // /// Gets a value of System.Boolean
    // /// </summary>
    // private bool requestsRecovery;
}
