// <copyright file="IJobDiagnosticDataProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

/// <summary>
/// DuckTyping interface for Quartz.Logging.JobDiagnosticData
/// </summary>
internal interface IJobDiagnosticDataProxy : IDuckType
{
    IITriggerProxy Trigger { get; }

    bool Recovering { get; }

    object RecoveringTriggerKey { get; }

    int RefireCount { get; }

    object MergedJobDataMap { get; }

    IIJobDetailProxy JobDetail { get; }

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
/// DuckTyping interface for Quartz.ITrigger
/// </summary>
internal interface IITriggerProxy : IDuckType
{
    IJobKeyProxy Key { get; }

    object JobKey { get; }

    string Description { get; }

    string CalendarName { get; }

    object JobDataMap { get; }

    DateTimeOffset? FinalFireTimeUtc { get; }

    int MisfireInstruction { get; }

    DateTimeOffset? EndTimeUtc { get; }

    DateTimeOffset StartTimeUtc { get; }

    int Priority { get; set; }

    bool HasMillisecondPrecision { get; }
}

/// <summary>
/// DuckTyping interface for Quartz.IJobDetail
/// </summary>
internal interface IIJobDetailProxy : IDuckType
{
    IJobKeyProxy Key { get; }

    string Description { get; }

    Type JobType { get; }

    object JobDataMap { get; }

    bool Durable { get; }

    bool PersistJobDataAfterExecution { get; }

    bool ConcurrentExecutionDisallowed { get; }

    bool RequestsRecovery { get; }
}

/// <summary>
/// DuckTyping interface for Quartz.JobKey
/// </summary>
internal interface IJobKeyProxy : IDuckType
{
    string Name { get; }

    string Group { get; }
}
