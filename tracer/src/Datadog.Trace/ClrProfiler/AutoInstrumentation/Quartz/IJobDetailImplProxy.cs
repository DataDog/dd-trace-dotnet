// <copyright file="IJobDetailImplProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

/// <summary>
/// DuckTyping interface for Quartz.Impl.JobDetailImpl
/// </summary>
internal interface IJobDetailImplProxy : IDuckType
{
    string Name { get; set; }

    string Group { get; set; }

    string FullName { get; }

    object Key { get; set; }

    string Description { get; set; }

    Type JobType { get; set; }

    object JobDataMap { get; set; }

    bool RequestsRecovery { get; set; }

    bool Durable { get; set; }

    bool PersistJobDataAfterExecution { get; }

    bool ConcurrentExecutionDisallowed { get; }
}
