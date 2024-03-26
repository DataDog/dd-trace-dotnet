// <copyright file="SerializableStatsBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

internal readonly struct SerializableStatsBucket
{
    /// <summary>
    /// The type of bucket this is
    /// </summary>
    public readonly TimestampType TimestampType;

    /// <summary>
    /// The start time of this bucket
    /// </summary>
    public readonly long BucketStartTimeNs;

    /// <summary>
    /// The stats bucket, keyed on <see cref="StatsBucket.Hash"/>
    /// </summary>
    public readonly Dictionary<ulong, StatsBucket> Bucket;

    public SerializableStatsBucket(
        TimestampType timestampType,
        long bucketStartTimeNs,
        Dictionary<ulong, StatsBucket> bucket)
    {
        TimestampType = timestampType;
        BucketStartTimeNs = bucketStartTimeNs;
        Bucket = bucket;
    }
}
