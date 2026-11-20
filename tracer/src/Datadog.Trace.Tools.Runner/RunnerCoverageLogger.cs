// <copyright file="RunnerCoverageLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Sends the messages of the coverage rewrite to the runner log.
/// </summary>
internal sealed class RunnerCoverageLogger : ICollectorLogger
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RunnerCoverageLogger));

    public void Error(string? text) => Log.Error<string?>("{Message}", text);

    public void Error(Exception exception) => Log.Error(exception, "Error rewriting an assembly for code coverage.");

    public void Error(Exception exception, string? text) => Log.Error<string?>(exception, "{Message}", text);

    public void Warning(string? text) => Log.Warning<string?>("{Message}", text);

    public void Debug(string? text) => Log.Debug<string?>("{Message}", text);
}
