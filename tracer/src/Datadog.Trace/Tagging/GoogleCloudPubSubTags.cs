// <copyright file="GoogleCloudPubSubTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

#nullable enable

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class GoogleCloudPubSubTags : InstrumentationTags
    {
        public GoogleCloudPubSubTags()
            : this(SpanKinds.Producer)
        {
        }

        public GoogleCloudPubSubTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => "GoogleCloudPubSub";

        [Tag(Trace.Tags.MessagingSystem)]
        public string MessagingSystem => "google-pubsub";

        [Tag(Trace.Tags.MessagingOperation)]
        public string? MessagingOperation { get; set; }

        [Tag(Trace.Tags.MessagingSourceName)]
        public string? MessagingSourceName { get; set; }

        [Tag(Trace.Tags.MessagingDestinationName)]
        public string? MessagingDestinationName { get; set; }

        [Tag(Trace.Tags.LegacyMessageBusDestination)]
        public string? LegacyMessageBusDestination { get; set; }

        [Tag(Trace.Tags.MessagingMessageId)]
        public string? MessagingMessageId { get; set; }

        [Metric(Trace.Metrics.MessageQueueTimeMs)]
        public double? MessageQueueTimeMs { get; set; }
    }

    internal sealed partial class GoogleCloudPubSubV1Tags : GoogleCloudPubSubTags
    {
        private string? _peerServiceOverride;

        public GoogleCloudPubSubV1Tags()
            : base()
        {
        }

        public GoogleCloudPubSubV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string? PeerService
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? MessagingDestinationName ?? LegacyMessageBusDestination;
            }
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride is not null
                            ? "peer.service"
                            : MessagingDestinationName is not null
                                ? Trace.Tags.MessagingDestinationName
                                : Trace.Tags.LegacyMessageBusDestination;
            }
        }
    }
}
#pragma warning restore SA1402 // File must contain single type
