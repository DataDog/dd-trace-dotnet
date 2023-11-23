// <copyright file="IBasicGetResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// BasicGetResult interface for ducktyping
    /// </summary>
    internal interface IBasicGetResult
    {
        /// <summary>
        /// Gets the message body of the result
        /// </summary>
        IBody? Body { get; }

        /// <summary>
        /// Gets the message properties
        /// </summary>
        IBasicProperties BasicProperties { get; }
    }
}
