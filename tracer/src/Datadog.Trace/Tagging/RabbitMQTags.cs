// <copyright file="RabbitMQTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class RabbitMQTags : InstrumentationTags
    {
        // For the sake of unit tests, define a default constructor with the default behavior,
        // though the RabbitMQ integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        public RabbitMQTags()
            : this(SpanKinds.Client)
        {
        }

        public RabbitMQTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName { get; set; }

        [Tag(Trace.Tags.AmqpCommand)]
        public string Command { get; set; }

        [Tag(Trace.Tags.AmqpDeliveryMode)]
        public string DeliveryMode { get; set; }

        [Tag(Trace.Tags.AmqpExchange)]
        public string Exchange { get; set; }

        [Tag(Trace.Tags.AmqpRoutingKey)]
        public string RoutingKey { get; set; }

        [Tag(Trace.Tags.MessageSize)]
        public string MessageSize { get; set; }

        [Tag(Trace.Tags.AmqpQueue)]
        public string Queue { get; set; }

        [Tag(Trace.Tags.OutHost)]
        public string OutHost { get; set; }
    }

    internal partial class RabbitMQV1Tags : RabbitMQTags
    {
        private string _peerServiceOverride = null;

        public RabbitMQV1Tags()
            : base()
        {
        }

        public RabbitMQV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? OutHost;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? Tags.PeerService
                           : SpanKind.Equals(SpanKinds.Client) || SpanKind.Equals(SpanKinds.Producer)
                               ? Tags.OutHost
                               : null;
            }
        }
    }
}
