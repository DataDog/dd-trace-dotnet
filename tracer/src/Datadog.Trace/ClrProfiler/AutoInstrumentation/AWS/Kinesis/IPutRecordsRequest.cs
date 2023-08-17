// <copyright file="IPutRecordsRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// PutRecordsRequest interface for duck typing.
    /// </summary>
    internal interface IPutRecordsRequest : IAmazonKinesisRequestWithStreamName
    {
        /// <summary>
        /// Gets or sets the Kinesis Records.
        /// </summary>
        List<IContainsData> Records { get; set; }
    }
}
