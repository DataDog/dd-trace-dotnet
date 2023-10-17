// <copyright file="SerializableBacklogBucket.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

internal readonly struct SerializableBacklogBucket
{
    /// <summary>
    /// The start time of this bucket
    /// </summary>
    public readonly long BucketStartTimeNs;

    /// <summary>
    /// The stats bucket, keyed on <see cref="StatsBucket.Hash"/>
    /// </summary>
    public readonly Dictionary<string, BacklogBucket> Bucket;

    public SerializableBacklogBucket(long bucketStartTimeNs, Dictionary<string, BacklogBucket> bucket)
    {
        BucketStartTimeNs = bucketStartTimeNs;
        Bucket = bucket;
    }
}
