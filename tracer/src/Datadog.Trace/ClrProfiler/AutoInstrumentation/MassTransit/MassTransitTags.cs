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

        [Tag(Trace.Tags.MassTransitDestinationAddress)]
        public string? DestinationAddress { get; set; }

        [Tag(Trace.Tags.MassTransitInputAddress)]
        public string? InputAddress { get; set; }

        [Tag(Trace.Tags.MassTransitMessageTypes)]
        public string? MessageTypes { get; set; }
    }
}
