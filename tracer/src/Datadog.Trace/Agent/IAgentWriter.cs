// <copyright file="IAgentWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal interface IAgentWriter
    {
        void WriteTrace(ArraySegment<Span> trace);

        Task<bool> Ping();

        Task FlushTracesAsync();

        Task FlushAndCloseAsync();
    }
}
