// <copyright file="DatadogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using BenchmarkDotNet.Loggers;
using Datadog.Trace.Ci;

namespace Datadog.Trace.BenchmarkDotNet;

/// <inheritdoc />
internal class DatadogLogger : ILogger
{
    /// <summary>
    /// Default DatadogLogger instance
    /// </summary>
    public static readonly ILogger Default = new DatadogLogger();

    public string Id => "DatadogLogger";

    public int Priority => 1;

    public void Write(LogKind logKind, string text)
    {
    }

    public void WriteLine()
    {
    }

    public void WriteLine(LogKind logKind, string text)
    {
    }

    public void Flush()
    {
        // Flush is called at the end of the process, we make sure we are closing both module and session.
        DatadogExporter.Default.TestModule.Close();
        var testSession = DatadogExporter.Default.TestSession;
        testSession.Tags.CommandExitCode = Environment.ExitCode.ToString();
        testSession.Close(Environment.ExitCode == 0 ? TestStatus.Pass : TestStatus.Fail);
    }
}
