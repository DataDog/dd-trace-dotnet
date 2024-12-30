// <copyright file="IBasicProperties.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// BasicProperties interface for ducktyping
    /// </summary>
    internal interface IBasicProperties : IReadOnlyBasicProperties
    {
        /// <summary>
        /// Gets or sets the headers of the message
        /// </summary>
        /// <returns>Message headers</returns>
        /// <remarks>Using <c>new</c> because <see cref="IReadOnlyBasicProperties.Headers"/> is readonly</remarks>
        new IDictionary<string, object>? Headers { get; set; }
    }
}
