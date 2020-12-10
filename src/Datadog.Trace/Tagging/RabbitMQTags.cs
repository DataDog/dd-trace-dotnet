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
                new Property<RabbitMQTags, string>("message.size", t => t.MessageSize, (t, v) => t.MessageSize = v));

        private string _spanKind = SpanKinds.Client;

        public override string SpanKind => _spanKind;

        public string InstrumentationName { get; set; }

        public string Command { get; set; }

        public string DeliveryMode { get; set; }

        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public string MessageSize { get; set; }

        public string Queue { get; set; }

        public void SetSpanKind(string spanKind)
        {
            _spanKind = spanKind;
        }

        protected override IProperty<string>[] GetAdditionalTags() => RabbitMQTagsProperties;
    }
}
