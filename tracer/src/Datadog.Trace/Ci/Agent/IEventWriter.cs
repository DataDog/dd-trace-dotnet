// <copyright file="IEventWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;

namespace Datadog.Trace.Ci.Agent
{
    /// <summary>
    /// Event Writer interface
    /// </summary>
    internal interface IEventWriter : IAgentWriter
    {
        void WriteEvent(IEvent @event);
    }
}
