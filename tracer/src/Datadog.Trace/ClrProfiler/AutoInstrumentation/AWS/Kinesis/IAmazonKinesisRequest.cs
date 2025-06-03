// <copyright file="IAmazonKinesisRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Interface for duck typing AmazonKinesisRequest implementations that have either a StreamName or StreamARN property.
    /// At least one of these properties must be set for the request to be valid.
    /// </summary>
    internal interface IAmazonKinesisRequest
    {
        /// <summary>
        /// Gets the Name of the Stream. This may be null if StreamARN is set instead.
        /// </summary>
        string? StreamName { get; }

        /// <summary>
        /// Gets the ARN of the Stream. This may be null if StreamName is set instead.
        /// </summary>
        string? StreamArn { get; }
    }
}
