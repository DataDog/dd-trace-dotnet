// <copyright file="AzureEventHubsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AzureEventHubsTags : InstrumentationTags
    {
        public AzureEventHubsTags()
            : this(SpanKinds.Producer)
        {
        }

        public AzureEventHubsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => "AzureEventHubs";

        [Tag(Trace.Tags.MessagingSystem)]
        public string MessagingSystem => "eventhubs";

        [Tag(Trace.Tags.MessagingOperation)]
        public string? MessagingOperation { get; set; }

        [Tag(Trace.Tags.MessagingSourceName)]
        public string? MessagingSourceName { get; set; }

        [Tag(Trace.Tags.MessagingDestinationName)]
        public string? MessagingDestinationName { get; set; }

        [Tag(Trace.Tags.LegacyMessageBusDestination)]
        public string? LegacyMessageBusDestination { get; set; }

        [Tag(Trace.Tags.NetworkDestinationName)]
        public string? NetworkDestinationName { get; set; }

        [Tag(Trace.Tags.NetworkDestinationPort)]
        public string? NetworkDestinationPort { get; set; }

        [Tag(Trace.Tags.ServerAddress)]
        public string? ServerAddress { get; set; }

        [Tag(Trace.Tags.MessagingBatchMessageCount)]
        public string? MessagingBatchMessageCount { get; set; }

        [Tag(Trace.Tags.MessagingMessageId)]
        public string? MessagingMessageId { get; set; }

        [Metric(Trace.Metrics.MessageQueueTimeMs)]
        public double? MessageQueueTimeMs { get; set; }
    }

    internal sealed partial class AzureEventHubsV1Tags : AzureEventHubsTags
    {
        private string? _peerServiceOverride = null;

        public AzureEventHubsV1Tags()
            : base()
        {
        }

        public AzureEventHubsV1Tags(string spanKind)
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
