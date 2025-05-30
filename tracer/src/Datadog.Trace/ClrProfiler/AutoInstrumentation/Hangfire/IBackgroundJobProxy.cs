// <copyright file="IBackgroundJobProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

// DuckTyping interface for Hangfire.BackgroundJob
// </summary>
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire;

internal interface IBackgroundJobProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.DateTime
    /// </summary>
    DateTime CreatedAt { get; }
}
