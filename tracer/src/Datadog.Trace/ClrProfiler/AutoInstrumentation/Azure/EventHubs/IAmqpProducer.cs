// <copyright file="IAmqpProducer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.Amqp.AmqpProducer
    /// </summary>
    internal interface IAmqpProducer : IDuckType
    {
        /// <summary>
        /// Gets the name of the Event Hub (private property in AmqpProducer)
        /// </summary>
        [Duck(Name = "EventHubName")]
        string EventHubName { get; }

        /// <summary>
        /// Gets the connection scope (private property in AmqpProducer)
        /// </summary>
        [Duck(Name = "ConnectionScope")]
        IAmqpConnectionScope ConnectionScope { get; }
    }

    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.Amqp.AmqpConnectionScope
    /// </summary>
    internal interface IAmqpConnectionScope : IDuckType
    {
        /// <summary>
        /// Gets the service endpoint for the Event Hubs service (private property)
        /// </summary>
        [Duck(Name = "ServiceEndpoint")]
        Uri? ServiceEndpoint { get; }

        /// <summary>
        /// Gets the name of the Event Hub (private property)
        /// </summary>
        [Duck(Name = "EventHubName")]
        string EventHubName { get; }
    }
}
