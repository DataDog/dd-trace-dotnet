// <copyright file="ConsoleExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Exporter;

internal sealed class ConsoleExporter : IAgentWriter
{
    public void WriteTrace(in SpanCollection trace)
    {
        // Eagerly return if the trace is empty
        if (trace.Count == 0)
        {
            return;
        }

        for (int i = 0; i < trace.Count; i++)
        {
            Console.WriteLine(trace[i]);
            Console.WriteLine();
        }
    }

    public Task<bool> Ping() => Task.FromResult(true);

    public Task FlushTracesAsync() => Task.CompletedTask;

    public Task FlushAndCloseAsync() => Task.CompletedTask;
}
