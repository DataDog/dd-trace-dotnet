// <copyright file="ISendMessageBatchRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// SendMessageBatchRequest interface for ducktyping
    /// </summary>
    public interface ISendMessageBatchRequest : IAmazonSQSRequestWithQueueUrl
    {
        /// <summary>
        /// Gets the message entries
        /// </summary>
        IList Entries { get; }
    }
}
