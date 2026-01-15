// <copyright file="IReceiveContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Duck-typing interface for MassTransit.ReceiveContext
    /// Only includes properties that are reliably available across MassTransit versions
    /// </summary>
    internal interface IReceiveContext
    {
        /// <summary>
        /// Gets the input address (queue/topic this message was received from)
        /// </summary>
        Uri? InputAddress { get; }

        /// <summary>
        /// Gets the transport headers for trace context propagation
        /// </summary>
        object? TransportHeaders { get; }
    }
}
