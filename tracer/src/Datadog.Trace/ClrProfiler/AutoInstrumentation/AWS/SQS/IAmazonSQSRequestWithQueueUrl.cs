// <copyright file="IAmazonSQSRequestWithQueueUrl.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// Interface for ducktyping AmazonSQSRequest implementations with the QueueUrl property
    /// </summary>
    [DuckType("Amazon.SQS.Model.DeleteMessageBatchRequest", "AWSSDK.SQS")]
    [DuckType("Amazon.SQS.Model.DeleteMessageRequest", "AWSSDK.SQS")]
    [DuckType("Amazon.SQS.Model.DeleteQueueRequest", "AWSSDK.SQS")]
    [DuckType("Amazon.SQS.Model.SendMessageBatchRequest", "AWSSDK.SQS")]
    [DuckType("Amazon.SQS.Model.SendMessageRequest", "AWSSDK.SQS")]
    internal interface IAmazonSQSRequestWithQueueUrl
    {
        /// <summary>
        /// Gets the URL of the queue
        /// </summary>
        string QueueUrl { get; }
    }
}
