// <copyright file="IServiceBusMessageBatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Duck type interface for Azure.Messaging.ServiceBus.ServiceBusMessageBatch
    /// </summary>
    internal interface IServiceBusMessageBatch : IDuckType
    {
        int Count { get; }

        [DuckField(Name = "_clientDiagnostics")]
        IMessagingClientDiagnostics ClientDiagnostics { get; }
    }
}
