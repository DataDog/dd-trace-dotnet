// <copyright file="DatadogSessionLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using BenchmarkDotNet.Loggers;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet;

/// <inheritdoc />
internal class DatadogSessionLogger : ILogger
{
    /// <summary>
    /// Default DatadogLogger instance
    /// </summary>
    public static readonly ILogger Default = new DatadogSessionLogger();

    public string Id => "DatadogSessionLogger";

    public int Priority => 1;

    public void Write(LogKind logKind, string text)
    {
    }

    public void WriteLine()
    {
    }

    public void WriteLine(LogKind logKind, string text)
    {
        if (text is null)
        {
            return;
        }

        switch (logKind)
        {
            case LogKind.Header when text.Contains("* Artifacts cleanup *"):
            case LogKind.Statistic when text.Contains("Global total time"):
                // Flush is called at the end of the process, we make sure we are closing both module and session.
                // This is done because BenchmarkDotNet doesn't have an event to know when has finished running benchmarks
                AsyncUtil.RunSync(DatadogExporter.Default.DisposeTestSessionAndModules);
                break;
        }
    }

    public void Flush()
    {
    }
}
