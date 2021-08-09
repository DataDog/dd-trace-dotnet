// <copyright file="ICreateQueueRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// CreateQueueRequest interface for ducktyping
    /// </summary>
    public interface ICreateQueueRequest
    {
        /// <summary>
        /// Gets the name of the queue
        /// </summary>
        string QueueName { get; }

        /// <summary>
        /// Gets the message attributes
        /// </summary>
        Dictionary<string, string> Attributes { get; }
    }
}
