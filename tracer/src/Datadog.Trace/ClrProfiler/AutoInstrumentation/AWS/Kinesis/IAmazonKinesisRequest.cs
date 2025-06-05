// <copyright file="IAmazonKinesisRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Base interface for duck typing AmazonKinesisRequest implementations that have a StreamName property.
    /// </summary>
    internal interface IAmazonKinesisRequest
    {
        /// <summary>
        /// Gets the Name of the Stream.
        /// </summary>
        string? StreamName { get; }
    }

    /// <summary>
    /// Extended interface for duck typing AmazonKinesisRequest implementations that also have a StreamARN property.
    /// Only available in Kinesis client v3.7+
    /// </summary>
    internal interface IAmazonKinesisRequestWithStreamARN : IAmazonKinesisRequest
    {
        /// <summary>
        /// Gets the ARN of the Stream. This may be null if StreamName is set instead.
        /// Only available in Kinesis client v3.7+
        /// </summary>
        string? StreamARN { get; }
    }
}
