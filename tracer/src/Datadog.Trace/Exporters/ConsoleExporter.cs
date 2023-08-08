// <copyright file="ConsoleExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;

namespace Datadog.Trace.Exporters;

internal class ConsoleExporter : IAgentWriter
{
    public void WriteTrace(ArraySegment<Span> spans)
    {
        // Eagerly return if the trace is empty
        if (spans.Count == 0)
        {
            return;
        }

        for (int i = 0; i < spans.Count; i++)
        {
            Console.WriteLine(spans.Array[i + spans.Offset]);
            Console.WriteLine();
        }
    }

    public Task<bool> Ping() => Task.FromResult(true);

    public Task FlushTracesAsync() => Task.CompletedTask;

    public Task FlushAndCloseAsync() => Task.CompletedTask;
}
