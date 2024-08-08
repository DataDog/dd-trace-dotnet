// <copyright file="KafkaTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class KafkaTags : InstrumentationTags
    {
        private const string ComponentName = "kafka";

        // For the sake of unit tests, define a default constructor
        // though the Kafka integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public KafkaTags()
            : this(InternalSpanKinds.Producer)
        {
        }

        public KafkaTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Internal.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Internal.Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        [Tag(Internal.Tags.KafkaBootstrapServers)]
        public string BootstrapServers { get; set; }

        [Tag(Internal.Tags.KafkaPartition)]
        public string Partition { get; set; }

        [Tag(Internal.Tags.KafkaOffset)]
        public string Offset { get; set; }

        [Tag(Internal.Tags.KafkaTombstone)]
        public string Tombstone { get; set; }

        [Tag(Internal.Tags.KafkaConsumerGroup)]
        public string ConsumerGroup { get; set; }

        [Metric(Trace.Metrics.MessageQueueTimeMs)]
        public double? MessageQueueTimeMs { get; set; }
    }

    internal partial class KafkaV1Tags : KafkaTags
    {
        private string _peerServiceOverride = null;

        // For the sake of unit tests, define a default constructor
        // though the Kafka integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public KafkaV1Tags()
            : this(InternalSpanKinds.Producer)
        {
        }

        public KafkaV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Internal.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? (SpanKind.Equals(InternalSpanKinds.Client) || SpanKind.Equals(InternalSpanKinds.Producer) ?
                       BootstrapServers
                       : null);
            private set => _peerServiceOverride = value;
        }

        [Tag(Internal.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? "peer.service"
                           : SpanKind.Equals(InternalSpanKinds.Client) || SpanKind.Equals(InternalSpanKinds.Producer) ?
                               Internal.Tags.KafkaBootstrapServers
                               : null;
            }
        }
    }
}
