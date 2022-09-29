using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Receive
{
    // Implement a custom consumer class whose implementation is nearly
    // identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
    class CustomInstrumentationConsumer : IBasicConsumer
    {
        private readonly DefaultBasicConsumer _consumer;
        private readonly IModel _model;

        public CustomInstrumentationConsumer(IModel model)
        {
            _consumer = new DefaultBasicConsumer(model);
            _model = model;
        }

        public IModel Model => _model;

        public event EventHandler<BasicDeliverEventArgs> Received;

        public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

        public void HandleBasicCancel(string consumerTag) =>
            _consumer.HandleBasicCancel(consumerTag);

        public void HandleBasicCancelOk(string consumerTag) => _consumer.HandleBasicCancelOk(consumerTag);

        public void HandleBasicConsumeOk(string consumerTag) => _consumer.HandleBasicConsumeOk(consumerTag);

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            _consumer.HandleBasicDeliver(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body);
            Received?.Invoke(
                this,
                new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }

        public void HandleModelShutdown(object model, ShutdownEventArgs reason) => _consumer.HandleModelShutdown(model, reason);
    }
}
