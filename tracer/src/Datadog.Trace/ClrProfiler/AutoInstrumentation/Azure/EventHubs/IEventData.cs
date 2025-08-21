// <copyright file="IEventData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.EventData
    /// </summary>
    internal interface IEventData : IDuckType
    {
        /// <summary>
        /// Gets the application properties associated with the event
        /// </summary>
        IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Gets the event body as BinaryData
        /// </summary>
        IBinaryData EventBody { get; }

        /// <summary>
        /// Gets the message ID
        /// </summary>
        string? MessageId { get; }

        /// <summary>
        /// Gets the partition key
        /// </summary>
        string? PartitionKey { get; }
    }

    /// <summary>
    /// Duck type for Azure.Core.BinaryData
    /// </summary>
    internal interface IBinaryData : IDuckType
    {
        /// <summary>
        /// Converts the BinaryData to a byte array
        /// </summary>
        byte[] ToArray();
    }
}
