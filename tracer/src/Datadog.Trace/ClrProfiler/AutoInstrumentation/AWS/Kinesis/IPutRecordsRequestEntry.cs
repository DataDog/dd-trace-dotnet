// <copyright file="IPutRecordsRequestEntry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// PutRecordsRequestEntry interface for duck typing.
    /// Mirrors Amazon.Kinesis.Model.PutRecordsRequestEntry.
    /// </summary>
    internal interface IPutRecordsRequestEntry : IContainsData
    {
        string? ExplicitHashKey { get; }

        string PartitionKey { get; }
    }
}
