// <copyright file="AzureEventGridTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AzureEventGridTags : InstrumentationTags
    {
        public AzureEventGridTags()
            : this(SpanKinds.Producer)
        {
        }

        public AzureEventGridTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => "AzureEventGrid";

        [Tag(Trace.Tags.MessagingSystem)]
        public string MessagingSystem => "eventgrid";

        [Tag(Trace.Tags.MessagingOperation)]
        public string? MessagingOperation { get; set; }

        [Tag(Trace.Tags.MessagingDestinationName)]
        public string? MessagingDestinationName { get; set; }

        [Tag(Trace.Tags.NetworkDestinationName)]
        public string? NetworkDestinationName { get; set; }

        [Tag(Trace.Tags.ServerAddress)]
        public string? ServerAddress { get; set; }

        [Tag(Trace.Tags.MessagingBatchMessageCount)]
        public string? MessagingBatchMessageCount { get; set; }
    }

    internal sealed partial class AzureEventGridV1Tags : AzureEventGridTags
    {
        private string? _peerServiceOverride;

        public AzureEventGridV1Tags()
            : base()
        {
        }

        public AzureEventGridV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        [Tag(Trace.Tags.PeerService)]
        public string? PeerService
        {
            get => _peerServiceOverride ?? MessagingDestinationName;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                    ? "peer.service"
                    : Trace.Tags.MessagingDestinationName;
            }
        }
    }
}
#pragma warning restore SA1402 // File must contain single type
