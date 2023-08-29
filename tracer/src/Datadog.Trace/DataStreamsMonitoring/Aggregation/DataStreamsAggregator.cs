// <copyright file="DataStreamsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.DataStreamsMonitoring.Utils;

namespace Datadog.Trace.DataStreamsMonitoring.Aggregation;

/// <summary>
/// Aggregates multiple <see cref="StatsPoint"/>s into their correct buckets
/// Note that this class is not thread safe
/// </summary>
internal class DataStreamsAggregator
{
    // The inner dictionary is constrained in size by the number of unique hashes seen by the app
    // Unique hashes are unique paths from origin to here, which could be unbounded if there are loops
    // The outer dictionary is constrained by FlushInterval/BucketDuration + 1
    private readonly Dictionary<long, Dictionary<ulong, StatsBucket>> _currentBuckets = new();

    // The inner dictionary is similarly constrained
    // but the outer dictionary is unconstrained, which could be problematic...
    private readonly Dictionary<long, Dictionary<ulong, StatsBucket>> _originBuckets = new();

    private readonly Dictionary<long, Dictionary<string, BacklogBucket>> _backlogBuckets = new();

    private readonly DataStreamsMessagePackFormatter _formatter;
    private readonly DDSketchPool _sketchPool = new();
    private readonly long _bucketDurationInNs;
    private List<SerializableStatsBucket>? _statsToWrite;
    private List<SerializableBacklogBucket>? _backlogsToWrite;

    public DataStreamsAggregator(DataStreamsMessagePackFormatter formatter, int bucketDurationMs)
    {
        _formatter = formatter;
        _bucketDurationInNs = ((long)bucketDurationMs) * 1_000_000;
    }

    /// <summary>
    /// Add the stats point to the aggregated stats
    /// </summary>
    public void Add(in StatsPoint point)
    {
        var currentBucketStartTime = BucketStartTimeForTimestamp(point.TimestampNs);
        AddToBuckets(point, currentBucketStartTime, _currentBuckets);

        var originTimestamp = point.TimestampNs - point.PathwayLatencyNs;
        var originBucketStartTime = BucketStartTimeForTimestamp(originTimestamp);
        AddToBuckets(point, originBucketStartTime, _originBuckets);
    }

    public void AddBacklog(in BacklogPoint point)
    {
        var currentBucketStartTime = BucketStartTimeForTimestamp(point.TimestampNs);
        if (!_backlogBuckets.TryGetValue(currentBucketStartTime, out var bucket))
        {
            bucket = new Dictionary<string, BacklogBucket>();
            _backlogBuckets[currentBucketStartTime] = bucket;
        }

        if (!bucket.TryGetValue(point.Tags, out var group) || group.Value < point.Value)
        {
            bucket[point.Tags] = new BacklogBucket(point.Tags, point.Value);
        }
    }

    /// <summary>
    /// Serialize the aggregated results using message pack
    /// </summary>
    /// <param name="stream">The buffer to write the stats into</param>
    /// <param name="maxBucketFlushTimeNs">Don't flush buckets that start (or include) this time (in Ns)</param>
    /// <returns>True if data was serialized to the stream</returns>
    public bool Serialize(Stream stream, long maxBucketFlushTimeNs)
    {
        var statsToAdd = Export(maxBucketFlushTimeNs) ?? new();
        var backlogsToAdd = ExportBacklogs(maxBucketFlushTimeNs) ?? new();
        if (statsToAdd.Count > 0 || backlogsToAdd.Count > 0)
        {
            _formatter.Serialize(stream, _bucketDurationInNs, statsToAdd, backlogsToAdd);
            Clear(statsToAdd, backlogsToAdd);

            return true;
        }

        return false;
    }

    internal List<SerializableBacklogBucket> ExportBacklogs(long maxBucketFlushTimeNs)
    {
        var currentBucketStart = maxBucketFlushTimeNs - _bucketDurationInNs;
        _backlogsToWrite ??= new List<SerializableBacklogBucket>(_backlogBuckets.Count);
        _backlogsToWrite.Clear();

        foreach (var kvp in _backlogBuckets)
        {
            if (kvp.Key > currentBucketStart)
            {
                continue;
            }

            _backlogsToWrite.Add(new SerializableBacklogBucket(kvp.Key, kvp.Value));
        }

        return _backlogsToWrite;
    }

    /// <summary>
    /// Exports the currently aggregated stats
    /// Internal for testing
    /// </summary>
    internal List<SerializableStatsBucket>? Export(long maxBucketFlushTimeNs)
    {
        // we don't have overlapping buckets so any buckets with a start time greater than
        // this must be "current"
        var currentBucketStart = maxBucketFlushTimeNs - _bucketDurationInNs;
        _statsToWrite ??= new List<SerializableStatsBucket>(_currentBuckets.Count + _originBuckets.Count);
        _statsToWrite.Clear();
        foreach (var kvp in _currentBuckets)
        {
            if (kvp.Key > currentBucketStart)
            {
                // don't flush the current bucket
                continue;
            }

            _statsToWrite.Add(new SerializableStatsBucket(TimestampType.Current, kvp.Key, kvp.Value));
        }

        foreach (var kvp in _originBuckets)
        {
            if (kvp.Key > currentBucketStart)
            {
                // don't flush the current bucket
                continue;
            }

            _statsToWrite.Add(new SerializableStatsBucket(TimestampType.Origin, kvp.Key, kvp.Value));
        }

        return _statsToWrite;
    }

    internal void Clear(List<SerializableStatsBucket> statsToRemove, List<SerializableBacklogBucket> backlogsToRemove)
    {
        // remove the old buckets
        foreach (var statsBucket in statsToRemove)
        {
            if (statsBucket.TimestampType == TimestampType.Current)
            {
                _currentBuckets.Remove(statsBucket.BucketStartTimeNs);
            }
            else
            {
                _originBuckets.Remove(statsBucket.BucketStartTimeNs);
            }

            // add the sketches back to the pool
            foreach (var bucket in statsBucket.Bucket)
            {
                _sketchPool.Release(bucket.Value.EdgeLatency);
                _sketchPool.Release(bucket.Value.PathwayLatency);
                _sketchPool.Release(bucket.Value.PayloadSize);
            }
        }

        foreach (var backlogBucket in backlogsToRemove)
        {
            _backlogBuckets.Remove(backlogBucket.BucketStartTimeNs);
        }
    }

    private void AddToBuckets(
        StatsPoint point,
        long currentBucketStartTime,
        Dictionary<long, Dictionary<ulong, StatsBucket>> buckets)
    {
        if (!buckets.TryGetValue(currentBucketStartTime, out var bucket))
        {
            bucket = new Dictionary<ulong, StatsBucket>();
            buckets[currentBucketStartTime] = bucket;
        }

        if (!bucket.TryGetValue(point.Hash.Value, out var group))
        {
            group = new StatsBucket(
                edgeTags: point.EdgeTags,
                hash: point.Hash,
                parentHash: point.ParentHash,
                pathwayLatency: _sketchPool.Get(),
                edgeLatency: _sketchPool.Get(),
                payloadSize: _sketchPool.Get());
            bucket[point.Hash.Value] = group;
        }

        var pathwayLatencySeconds = Convert.ToDouble(point.PathwayLatencyNs) / 1_000_000_000;
        group.PathwayLatency.Add(Math.Max(pathwayLatencySeconds, 0));

        var edgeLatencySeconds = Convert.ToDouble(point.EdgeLatencyNs) / 1_000_000_000;
        group.EdgeLatency.Add(Math.Max(edgeLatencySeconds, 0));
        group.PayloadSize.Add(point.PayloadSizeBytes);
    }

    /// <summary>
    /// Gets the provided timestamp truncated to the bucket size.
    /// It gives us the start time of the time bucket in which such timestamp falls.
    /// </summary>
    private long BucketStartTimeForTimestamp(long timestampNs)
    {
        return timestampNs - (timestampNs % _bucketDurationInNs);
    }
}
