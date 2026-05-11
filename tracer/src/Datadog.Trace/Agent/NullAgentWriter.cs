// <copyright file="NullAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    /// <summary>
    /// No-op agent writer for standalone IAST use: spans are created and managed in-process
    /// (so request lifecycle and taint tracking work) but are never transmitted to any agent.
    /// </summary>
    internal sealed class NullAgentWriter : IAgentWriter
    {
        public static readonly NullAgentWriter Instance = new();

        private NullAgentWriter()
        {
        }

        public void WriteTrace(in SpanCollection trace)
        {
        }

        public Task<bool> Ping() => Task.FromResult(true);

        public Task FlushTracesAsync() => Task.CompletedTask;

        public Task FlushAndCloseAsync() => Task.CompletedTask;
    }
}
