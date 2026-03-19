// <copyright file="MassTransitTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal sealed partial class MassTransitTags : InstrumentationTags
    {
        // For the sake of unit tests, define a default constructor
        // though the MassTransit integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public MassTransitTags()
            : this(SpanKinds.Producer)
        {
        }

        public MassTransitTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => MassTransitConstants.ComponentTagName;

        [Tag(Trace.Tags.MassTransitMessagingOperation)]
        public string? MessagingOperation { get; set; }

        [Tag(Trace.Tags.MassTransitMessagingSystem)]
        public string? MessagingSystem { get; set; }

        [Tag(Trace.Tags.MassTransitDestinationName)]
        public string? DestinationName { get; set; }

        [Tag(Trace.Tags.MassTransitMessageId)]
        public string? MessageId { get; set; }

        [Tag(Trace.Tags.MassTransitConversationId)]
        public string? ConversationId { get; set; }

        [Tag(Trace.Tags.MassTransitCorrelationId)]
        public string? CorrelationId { get; set; }

        [Tag(Trace.Tags.MassTransitSourceAddress)]
        public string? SourceAddress { get; set; }

        [Tag(Trace.Tags.MassTransitDestinationAddress)]
        public string? DestinationAddress { get; set; }

        [Tag(Trace.Tags.MassTransitInputAddress)]
        public string? InputAddress { get; set; }

        [Tag(Trace.Tags.MassTransitMessageTypes)]
        public string? MessageTypes { get; set; }

        [Tag(Trace.Tags.MassTransitMessageSize)]
        public string? MessageSize { get; set; }

        [Tag(Trace.Tags.MassTransitInitiatorId)]
        public string? InitiatorId { get; set; }

        [Tag(Trace.Tags.MassTransitRequestId)]
        public string? RequestId { get; set; }

        [Tag(Trace.Tags.MassTransitResponseAddress)]
        public string? ResponseAddress { get; set; }

        [Tag(Trace.Tags.MassTransitFaultAddress)]
        public string? FaultAddress { get; set; }

        [Tag(Trace.Tags.MassTransitBeginState)]
        public string? BeginState { get; set; }

        [Tag(Trace.Tags.MassTransitEndState)]
        public string? EndState { get; set; }

        [Tag(Trace.Tags.MassTransitSagaId)]
        public string? SagaId { get; set; }

        [Tag(Trace.Tags.MassTransitSagaType)]
        public string? SagaType { get; set; }

        [Tag(Trace.Tags.MassTransitPeerAddress)]
        public string? PeerAddress { get; set; }

        [Tag(Trace.Tags.MassTransitConsumerType)]
        public string? ConsumerType { get; set; }
    }
}
