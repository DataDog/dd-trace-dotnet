// <copyright file="ILogEventCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

/// <summary>
/// Creates DirectSubmissionLogEvent instances from ILogger calls
/// </summary>
internal interface ILogEventCreator
{
    LoggerDirectSubmissionLogEvent CreateLogEvent<TState>(
        int logLevel,
        string categoryName,
        object eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter);

    IDisposable BeginScope<TState>(TState state);
}
