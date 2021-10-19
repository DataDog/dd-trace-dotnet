// <copyright file="KafkaTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.SourceGenerators;

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
            : this(SpanKinds.Producer)
        {
        }

        public KafkaTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [TagName(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [TagName(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => ComponentName;

        [TagName(Trace.Tags.KafkaPartition)]
        public string Partition { get; set; }

        [TagName(Trace.Tags.KafkaOffset)]
        public string Offset { get; set; }

        [TagName(Trace.Tags.KafkaTombstone)]
        public string Tombstone { get; set; }

        [MetricName(Trace.Metrics.MessageQueueTimeMs)]
        public double? MessageQueueTimeMs { get; set; }
    }
}
