// <copyright file="RabbitMQTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class RabbitMQTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] RabbitMQTagsProperties =
            InstrumentationTagsProperties.Concat(
                new Property<RabbitMQTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v),
                new Property<RabbitMQTags, string>(Trace.Tags.AmqpCommand, t => t.Command, (t, v) => t.Command = v),
                new Property<RabbitMQTags, string>(Trace.Tags.AmqpDeliveryMode, t => t.DeliveryMode, (t, v) => t.DeliveryMode = v),
                new Property<RabbitMQTags, string>(Trace.Tags.AmqpExchange, t => t.Exchange, (t, v) => t.Exchange = v),
                new Property<RabbitMQTags, string>(Trace.Tags.AmqpQueue, t => t.Queue, (t, v) => t.Queue = v),
                new Property<RabbitMQTags, string>(Trace.Tags.AmqpRoutingKey, t => t.RoutingKey, (t, v) => t.RoutingKey = v),
                new Property<RabbitMQTags, string>(Trace.Tags.MessageSize, t => t.MessageSize, (t, v) => t.MessageSize = v));

        private string _spanKind;

        // For the sake of unit tests, define a default constructor with the default behavior,
        // though the RabbitMQ integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        public RabbitMQTags()
        {
            _spanKind = SpanKinds.Client;
        }

        public RabbitMQTags(string spanKind)
        {
            _spanKind = spanKind;
        }

        public override string SpanKind => _spanKind;

        public string InstrumentationName { get; set; }

        public string Command { get; set; }

        public string DeliveryMode { get; set; }

        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public string MessageSize { get; set; }

        public string Queue { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => RabbitMQTagsProperties;
    }
}
