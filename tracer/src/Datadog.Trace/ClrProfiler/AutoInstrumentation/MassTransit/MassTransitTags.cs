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
    }
}
