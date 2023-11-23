// <copyright file="IBody.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// Body interface for ducktyping
    /// </summary>
    internal interface IBody
    {
        /// <summary>
        /// Gets the length of the message body
        /// </summary>
        int Length { get; }
    }
}
