// <copyright file="IEventHubProducerClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.Producer.EventHubProducerClient
    /// </summary>
    internal interface IEventHubProducerClient : IDuckType
    {
        /// <summary>
        /// Gets the name of the Event Hub
        /// </summary>
        string EventHubName { get; }

        /// <summary>
        /// Gets the fully qualified namespace
        /// </summary>
        string FullyQualifiedNamespace { get; }

        /// <summary>
        /// Gets the connection for the Event Hub (private property access)
        /// </summary>
        IEventHubConnection Connection { get; }
    }

    internal interface IEventHubConnection
    {
        /// <summary>
        /// Gets the service endpoint for the Event Hub (internal property access)
        /// </summary>
        Uri? ServiceEndpoint { get; }
    }
}
