// <copyright file="EventHubTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    internal partial class EventHubProducerTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Producer;

        [Tag("eventhub.name")]
        public string? EventHubName { get; set; }

        [Tag("eventhub.namespace")]
        public string? Namespace { get; set; }

        [Tag("messaging.operation")]
        public string? Operation { get; set; }
    }

    internal partial class EventHubConsumerTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Consumer;

        [Tag("eventhub.name")]
        public string? EventHubName { get; set; }

        [Tag("eventhub.namespace")]
        public string? Namespace { get; set; }

        [Tag("eventhub.consumer_group")]
        public string? ConsumerGroup { get; set; }

        [Tag("messaging.operation")]
        public string? Operation { get; set; }
    }

    internal partial class EventHubProcessorTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Consumer;

        [Tag("eventhub.name")]
        public string? EventHubName { get; set; }

        [Tag("eventhub.namespace")]
        public string? Namespace { get; set; }

        [Tag("eventhub.consumer_group")]
        public string? ConsumerGroup { get; set; }

        [Tag("eventhub.partition_id")]
        public string? PartitionId { get; set; }

        [Tag("messaging.operation")]
        public string? Operation { get; set; }

        [Tag("messaging.message_id")]
        public string? MessageId { get; set; }
    }
}
#pragma warning restore SA1402 // File must contain single type
