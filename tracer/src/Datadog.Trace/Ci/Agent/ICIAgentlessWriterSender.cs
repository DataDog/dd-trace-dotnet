// <copyright file="ICIAgentlessWriterSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Ci.Agent.Payloads;

namespace Datadog.Trace.Ci.Agent
{
    internal interface ICIAgentlessWriterSender
    {
        Task<bool> Ping();

        Task SendPayloadAsync(EventsPayload payload);
    }
}
