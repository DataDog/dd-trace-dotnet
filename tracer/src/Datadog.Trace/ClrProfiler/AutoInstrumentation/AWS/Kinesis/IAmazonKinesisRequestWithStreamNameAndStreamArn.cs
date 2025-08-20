// <copyright file="IAmazonKinesisRequestWithStreamNameAndStreamArn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Interface for Kinesis requests that have both StreamName and StreamARN properties
    /// </summary>
    internal interface IAmazonKinesisRequestWithStreamNameAndStreamArn : IAmazonKinesisRequestWithStreamName
    {
        /// <summary>
        /// Gets or sets the stream ARN
        /// </summary>
        string? StreamARN { get; set; }
    }
}
