// <copyright file="IAmazonSNSRequestWithTopicArn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    /// <summary>
    /// Interface for ducktyping AmazonSNSRequest implementations with the TopicArn property
    /// </summary>
    internal interface IAmazonSNSRequestWithTopicArn
    {
        /// <summary>
        /// Gets the Amazon Resource Name (ARN) of the topic
        /// </summary>
        string? TopicArn { get; }
    }
}
