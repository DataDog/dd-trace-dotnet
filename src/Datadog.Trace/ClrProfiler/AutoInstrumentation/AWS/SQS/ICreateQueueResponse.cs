// <copyright file="ICreateQueueResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// CreateQueueResponse interface for ducktyping
    /// </summary>
    public interface ICreateQueueResponse : IAmazonWebServiceResponse
    {
        /// <summary>
        /// Gets the URL of the created Amazon SQS queue
        /// </summary>
        string QueueUrl { get; }
    }
}
