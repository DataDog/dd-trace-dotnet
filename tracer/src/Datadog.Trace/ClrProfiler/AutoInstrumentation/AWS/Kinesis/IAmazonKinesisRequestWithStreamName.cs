// <copyright file="IAmazonKinesisRequestWithStreamName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// Interface for duck typing AmazonKinesisRequest implementations with the StreamName property
    /// </summary>
    internal interface IAmazonKinesisRequestWithStreamName
    {
        /// <summary>
        /// Gets the Name of the Stream
        /// </summary>
        string? StreamName { get; }
    }
}
