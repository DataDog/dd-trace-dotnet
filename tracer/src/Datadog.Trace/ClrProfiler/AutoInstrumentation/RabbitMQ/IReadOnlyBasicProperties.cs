// <copyright file="IReadOnlyBasicProperties.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;

/// <summary>
/// DuckType for BasicProperties which can also be used for generic IReadOnlyBasicProperties implementations
/// i.e. the headers aren't settable
/// </summary>
internal interface IReadOnlyBasicProperties
{
    /// <summary>
    /// Gets the headers of the message
    /// </summary>
    /// <returns>Message headers</returns>
    IDictionary<string, object>? Headers { get; }

    /// <summary>
    /// Gets the delivery mode of the message
    /// </summary>
    byte DeliveryMode { get; }

    /// <summary>
    /// Gets timestamp at which the message was produced
    /// </summary>
    AmqpTimestamp Timestamp { get; }

    /// <summary>
    /// Returns true if the DeliveryMode property is present
    /// </summary>
    /// <returns>true if the DeliveryMode property is present</returns>
    bool IsDeliveryModePresent();
}
