// <copyright file="DnsClientTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class DnsClientTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.DnsClient);

        [Tag(Trace.Tags.DnsQuestionName)]
        public string? QuestionName { get; set; }

        [Tag(Trace.Tags.DnsQuestionType)]
        public string? QuestionType { get; set; }

        [Tag(Trace.Tags.DnsQuestionClass)]
        public string? QuestionClass { get; set; }

        [Tag(Trace.Tags.DnsResponseCode)]
        public string? ResponseCode { get; set; }

        [Tag(Trace.Tags.DnsAnswerCount)]
        public string? AnswerCount { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string? OutHost { get; set; }

        [Tag(Trace.Tags.NetworkDestinationPort)]
        public string? DestinationPort { get; set; }
    }

    internal sealed partial class DnsClientV1Tags : DnsClientTags
    {
        private string? _peerServiceOverride;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string? PeerService
        {
            get => _peerServiceOverride ?? OutHost;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? Trace.Tags.PeerService
                        : OutHost is not null
                            ? Trace.Tags.OutHost
                            : null;
            }
        }
    }
}
