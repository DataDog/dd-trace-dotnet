// <copyright file="MassTransitTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal sealed partial class MassTransitTags : InstrumentationTags
    {
        public MassTransitTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => MassTransitConstants.IntegrationName;

        [Tag("messaging.operation")]
        public string MessagingOperation { get; set; }

        [Tag("messaging.system")]
        public string MessagingSystem { get; set; }

        [Tag("messaging.destination.name")]
        public string DestinationName { get; set; }

        [Tag("messaging.masstransit.message_id")]
        public string MessageId { get; set; }

        [Tag("messaging.message.conversation_id")]
        public string ConversationId { get; set; }

        [Tag("messaging.masstransit.source_address")]
        public string SourceAddress { get; set; }

        [Tag("messaging.masstransit.destination_address")]
        public string DestinationAddress { get; set; }

        // MT8 OTEL has input_address on receive spans
        [Tag("messaging.masstransit.input_address")]
        public string InputAddress { get; set; }

        [Tag("messaging.masstransit.message_types")]
        public string MessageTypes { get; set; }

        [Tag("messaging.message.body.size")]
        public string MessageSize { get; set; }

        [Tag("messaging.masstransit.initiator_id")]
        public string InitiatorId { get; set; }

        [Tag("messaging.masstransit.request_id")]
        public string RequestId { get; set; }

        [Tag("messaging.masstransit.response_address")]
        public string ResponseAddress { get; set; }

        [Tag("messaging.masstransit.fault_address")]
        public string FaultAddress { get; set; }

        // Saga state machine tags (matching MT8 OTEL instrumentation)
        [Tag("messaging.masstransit.begin_state")]
        public string BeginState { get; set; }

        [Tag("messaging.masstransit.end_state")]
        public string EndState { get; set; }

        [Tag("messaging.masstransit.correlation_id")]
        public string CorrelationId { get; set; }

        [Tag("messaging.masstransit.saga_id")]
        public string SagaId { get; set; }

        // Additional MT8 OTEL tags
        [Tag("peer.address")]
        public string PeerAddress { get; set; }

        [Tag("messaging.masstransit.consumer_type")]
        public string ConsumerType { get; set; }
    }
}
