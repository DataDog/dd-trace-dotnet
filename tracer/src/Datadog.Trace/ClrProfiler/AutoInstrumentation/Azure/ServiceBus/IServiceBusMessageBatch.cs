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
        /// <summary>
        /// Gets the maximum size allowed for the batch, in bytes.
        /// </summary>
        long MaxSizeInBytes { get; }

        /// <summary>
        /// Gets the size of the batch, in bytes, as it will be sent to the Queue/Topic.
        /// </summary>
        long SizeInBytes { get; }

        /// <summary>
        /// Gets the count of messages contained in the batch.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the client diagnostics instance that contains entity path information.
        /// </summary>
        [DuckField(Name = "_clientDiagnostics")]
        IMessagingClientDiagnostics? ClientDiagnostics { get; }
    }
}
