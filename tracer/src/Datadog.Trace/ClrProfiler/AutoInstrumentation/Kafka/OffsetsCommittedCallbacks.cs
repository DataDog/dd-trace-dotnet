// <copyright file="OffsetsCommittedCallbacks.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal readonly struct OffsetsCommittedCallbacks : IBegin2Callbacks, IVoidReturnCallback
    {
        public OffsetsCommittedCallbacks(string? groupId)
        {
            GroupId = groupId;
        }

        public string? GroupId { get; }

        public void OnException(object? sender, Exception ex)
        {
        }

        public void OnDelegateEnd(object? sender, Exception? exception, object? state)
        {
        }

        public object? OnDelegateBegin<TConsumer, TResult>(object? sender, ref TConsumer consumer, ref TResult result)
        {
            if (result.TryDuckCast<ICommittedOffsets>(out var committedOffsets))
            {
                var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;

                // Get cluster_id from ConsumerCache when the callback is invoked
                string? clusterId = null;
                if (sender != null)
                {
                    ConsumerCache.TryGetConsumerGroup(sender, out var _, out var _, out clusterId);
                }

                for (var i = 0; i < committedOffsets?.Offsets.Count; i++)
                {
                    var item = committedOffsets.Offsets[i];
                    var backlogTags = $"consumer_group:{GroupId},partition:{item.Partition.Value},topic:{item.Topic},type:kafka_commit";
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        backlogTags = $"kafka_cluster_id:{clusterId},{backlogTags}";
                    }

                    dataStreams.TrackBacklog(backlogTags, item.Offset.Value);
                }
            }

            return null;
        }
    }
}
